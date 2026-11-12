using System.Collections;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies.Desert;
using SignalLost.Inventory;
using SignalLost.Melee;
using UnityEngine;

namespace SignalLost.Enemies.Caves
{
    /// <summary>
    /// One of the four Crystal Caves beetle mini-bosses (Bug Lennon, Paul Arthropodney, George Harrison and Scarab
    /// Starr; docs/cave-beetles.md): would-be rock stars with microphone packs and headset mics. Built on
    /// <see cref="DuelBoss"/> like the other mini-bosses (Blender model and clips from tools/blender/cave_beetle.py,
    /// poise, leash and reset), and coordinated by <see cref="BeetleBand"/>: one shared boss bar, phases that follow the
    /// whole band's health, one attacker at a time, and the box-in.
    ///
    /// Phase 1 (Rehearsal): head-butt and the box-in. Phase 2 (Performance) adds the drum stomp (a shockwave of crystals
    /// to jump). Phase 3 (Encore) adds the stage dive (a shadow follows the player, then a belly-flop). A box-in crash or a
    /// missed stage dive flips it onto its back: the belly is the weak spot until it rights itself. The crash itself does
    /// no damage. Its hard shell shrugs off laser swings and fists, and takes a quarter of a jump smash; charged bolts hurt
    /// it fully. The last beetle standing drops the signal amplifier.
    /// </summary>
    public sealed class CaveBeetle : DuelBoss
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private enum Stage { None, Gather, Brace, Charge }

        [Header("Voice")]
        [Tooltip("Said in order, one per hit (when no one else is talking), so the band's story carries on through the fight.")]
        [SerializeField] private string[] hitLines = { "Ow!" };
        [SerializeField] private string lastWords = "Ow.";

        [Header("Moves")]
        [SerializeField, Min(0f)] private float headbuttDamage = 15f;
        [SerializeField, Min(0f)] private float stompDamage = 25f;
        [Tooltip("The crystal shockwave from the drum stomp: jump or dodge it.")]
        [SerializeField, Min(0f)] private float shockwaveDamage = 25f;
        [SerializeField, Min(2f)] private float shockwaveRadius = 11f;
        [SerializeField, Min(0f)] private float diveDamage = 28f;
        [SerializeField, Min(0.5f)] private float diveRadius = 2.6f;
        [Tooltip("Share of the dive's flight its shadow follows the player before it locks.")]
        [SerializeField, Range(0f, 1f)] private float diveTracking = 0.6f;
        [Tooltip("Its own crystal colour: the stomp's shockwave.")]
        [SerializeField] private Material crystalMaterial;

        [Header("Box-in charge")]
        [SerializeField, Min(1f)] private float chargeSpeed = 14f;
        [Tooltip("Flat distance from the player at which a charging beetle hits (or passes under) them.")]
        [SerializeField, Min(0.5f)] private float contactDistance = 1.9f;

        [Header("Loot")]
        [Tooltip("Scrap in the loot pile the last beetle standing drops (one pile for the band, like the Mudlands mini-bosses).")]
        [SerializeField, Min(0)] private int scrapDrop = 12;

        [Header("Hard shell")]
        [Tooltip("Share of a jump smash's damage the shell lets through (laser swings and fists do nothing; bolts do full damage).")]
        [SerializeField, Range(0f, 1f)] private float smashShare = 0.25f;

        [Header("Flipped (weak spot)")]
        [SerializeField, Min(0.5f)] private float flipTime = 3f;
        [Tooltip("The belly: one hit on it while flipped takes this many times the damage, then it rolls straight back over.")]
        [SerializeField] private Health belly;
        [SerializeField, Min(1f)] private float bellyMultiplier = 2.5f;
        [Tooltip("The shell's hitbox; off while flipped so hits land on the belly.")]
        [SerializeField] private Collider bodyHitbox;

        public BeetleBand Band { get; set; }
        public string[] HitLines => hitLines;
        public string LastWords => lastWords;
        public float ChargeSpeed => chargeSpeed;
        public float ContactDistance => contactDistance;
        public bool InFormation => stage != Stage.None;
        public bool Charging => stage == Stage.Charge;
        /// <summary>Free to be called into a box-in: between moves, not roaring or flipped.</summary>
        public bool ReadyForFormation => !IsDead && Engaged && !InPhaseShift && !Busy;
        /// <summary>In one of its own attacks (only one beetle attacks at a time).</summary>
        public bool Attacking => CurrentMove == "Headbutt" || CurrentMove == "Stomp" || CurrentMove == "StageDive";
        public bool Flipped { get; private set; }
        public bool PassedUnder { get; private set; }
        public bool Bumped { get; private set; }

        // Box-in and flips run as moves; hyper armour so a stagger can't knock a beetle out of formation.
        private readonly Attack boxInMove = new Attack { Name = "BoxIn", HyperArmor = true };
        private readonly Attack flipMove = new Attack { Name = "Flipped", HyperArmor = true };
        private Stage stage;
        private Vector3 slot, chargeAt;
        private float currentChargeSpeed, calmUntil;
        private Collider[] ownColliders = new Collider[0];
        private bool ignoringPlayer;
        private Material micLight;
        private Material[] crystals = new Material[0];
        private Color[] crystalGlow = new Color[0];
        private bool bellyStruck;
        private int struckFrame;
        private PlayerFists fists;
        private JumpSmash smash;

        protected override float TempoBonus => Band != null ? Band.SpeedScale : 1f;
        protected override float PhaseHealth => Band != null ? Band.HealthFraction : Health.Normalized;
        protected override string BannerName => Band != null ? Band.DisplayName : DisplayName;
        protected override string PartStage => ShardMole.Stage;
        // All four roar at once (engage, phase changes): one short push from the nearest, not four stacked ones.
        protected override float RoarShove => Band == null ? 3f : Target != null && Band.Nearest(Target.position) == this ? 3f : 0f;
        public override bool ShowsBossBar => false; // the band shows one bar for all four

        protected override void OnBossStarted()
        {
            base.OnBossStarted();
            ownColliders = GetComponentsInChildren<Collider>(true);
            RelayPart(belly, 0);
            if (Target != null)
            {
                fists = Target.GetComponentInParent<PlayerFists>();
                smash = Target.GetComponentInParent<JumpSmash>();
            }
            Health.DamageModifier = ShellDamage;
            // Their own copies, so each beetle's mic light and crystals glow on their own.
            var glows = new System.Collections.Generic.List<Material>();
            foreach (SkinnedMeshRenderer skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach (Material m in skin.materials)
                {
                    if (m.name.Contains("MicLight")) micLight = m;
                    else if (m.name.Contains("Crystal")) glows.Add(m);
                }
            crystals = glows.ToArray();
            crystalGlow = new Color[crystals.Length];
            for (int i = 0; i < crystals.Length; i++)
            {
                crystals[i].EnableKeyword("_EMISSION");
                crystalGlow[i] = crystals[i].GetColor(EmissionColor);
            }
        }

        protected override void DefineAttacks()
        {
            System.Func<bool> myTurn = () => Time.time >= calmUntil && (Band == null || Band.MayAttack(this));
            Attacks.Add(new Attack { Name = "Headbutt", Run = Headbutt, MaxRange = 3.8f, MaxAngle = 45f, Weight = 3f, Cooldown = 1.4f, When = myTurn });
            Attacks.Add(new Attack { Name = "Stomp", Run = Stomp, MaxRange = 9f, MinPhase = 2, Weight = 1.6f, Cooldown = 7f, When = myTurn, HyperArmor = true });
            Attacks.Add(new Attack { Name = "StageDive", Run = StageDive, MinRange = 4f, MaxRange = 16f, MinPhase = 3, Weight = 2f, Cooldown = 9f, When = myTurn, HyperArmor = true });
        }

        // ---------- The band ----------

        /// <summary>Joins the fight when a bandmate does.</summary>
        public void JoinFight() => Engage();

        protected override void OnEngaged()
        {
            base.OnEngaged();
            Band?.MemberEngaged(this);
        }

        protected override void TickCombat(float dt)
        {
            // The band drives the box-in, and a flipped beetle waits to get up; a new phase's roar waits for both.
            if (stage != Stage.None || Flipped)
            {
                UpdateShove(dt);
                return;
            }
            if (Band != null)
            {
                int want = 1;
                foreach (float at in PhaseMarks)
                    if (Band.HealthFraction <= at) want++;
                if (want > DuelPhase) RequestPhase(want);
            }
            base.TickCombat(dt);
            // Between moves it stalks the player, but never out of the band's zone.
            if (Band != null && !Busy && AgentReady && Agent.hasPath)
            {
                Vector3 goal = Agent.destination, kept = Band.KeepInZone(goal);
                if ((kept - goal).sqrMagnitude > 0.25f) Agent.SetDestination(kept);
            }
        }

        protected override void OnBossHit(float amount, GameObject source, Vector3 away)
        {
            base.OnBossHit(amount, source, away);
            Band?.MemberHit(this);
        }

        protected override float OnPartHit(int part, float amount, GameObject source)
        {
            if (!Flipped) return amount;
            // One hit on the belly, then it rolls back over (OnBack sees this once the hit's damage has landed).
            bellyStruck = true;
            struckFrame = Time.frameCount;
            return amount * bellyMultiplier;
        }

        /// <summary>
        /// The hard shell: the player's laser swings and fists do nothing, a jump smash does a quarter, charged bolts (and
        /// anything else) do full damage. Flipped, everything gets through (the belly hits add their bonus on top).
        /// </summary>
        private float ShellDamage(float amount, GameObject source)
        {
            if (Flipped || source == null || Target == null || source.transform.root != Target.root) return amount;
            if (smash != null && smash.IsLanding) return amount * smashShare;
            if (IsMeleeHit(source) || (fists != null && fists.IsBusy))
            {
                Clank();
                return 0f;
            }
            return amount;
        }

        private float lastClank;

        /// <summary>A melee hit glancing off the shell: a loud clang and sparks, so the player knows it landed but did nothing.</summary>
        private void Clank()
        {
            if (Time.time - lastClank < 0.12f) return; // one per swing, however many hitboxes it touched
            lastClank = Time.time;
            Vector3 toward = Target != null ? FlatDirectionTo(Target.position) : transform.forward;
            Vector3 point = BonePosition("Body") + toward * 0.8f + Vector3.up * 0.3f;
            Sfx.PlayAt("crab_block", point, 1f, Random.Range(0.62f, 0.72f), 35f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, point, toward + Vector3.up * 0.5f, 18, 0.06f, 6.5f, 50f, new Color(1f, 0.92f, 0.75f));
            ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, point, Vector3.zero, 3, 0.25f, 0.3f, 90f, new Color(1f, 0.95f, 0.85f, 0.5f));
        }

        protected override void OnMoveEnded()
        {
            stage = Stage.None;
            IgnorePlayer(false);
            SetFlipped(false);
        }

        protected override void OnDuelReset()
        {
            calmUntil = 0f;
            Band?.MemberReset(this);
        }

        protected override void OnDuelDeath()
        {
            // Only the last beetle standing drops the band's loot: the signal amplifier and one scrap pile.
            bool last = Band == null || !Band.OthersAlive(this);
            if (!last && ShipPart != null)
            {
                Destroy(ShipPart.gameObject);
                ShipPart = null;
            }
            if (last)
                for (int i = 0; i < scrapDrop; i++)
                {
                    ScrapType type = i % 4 == 0 ? ScrapType.Wiring : i % 5 == 0 ? ScrapType.CircuitBoards : i % 7 == 0 ? ScrapType.FuelCells : ScrapType.ScrapMetal;
                    Vector2 spread = Random.insideUnitCircle * 2.5f;
                    ScrapPickup.Spawn(type, transform.position + Vector3.up * 1.5f, new Vector3(spread.x, Random.Range(4f, 6f), spread.y));
                }
            Band?.MemberDown(this);
        }

        // ---------- Phase 1: head-butt ----------

        private IEnumerator Headbutt()
        {
            LookDuringMove(true);
            Play("Headbutt");
            float hit = Moment("hit");
            // Rearing back is the warning.
            yield return Until("move", 0, () => CloseIn(2.8f, 220f));
            Sfx.PlayAt("chain_swish", transform.position, 0.8f, 0.9f, 35f);
            yield return StrikeWindow(hit - 0.08f, hit + 0.12f, () => BonePosition("Head") + transform.forward * 0.4f, 1.6f, headbuttDamage, 2.5f,
                false, () => Lurch(7f));
            yield return Finish(() => Track(60f));
        }

        // ---------- Phase 2: drum stomp ----------

        private IEnumerator Stomp()
        {
            Play("Stomp");
            yield return Until("slam", 0, () => Track(120f));
            Vector3 ground = GroundBelow(transform.position + transform.forward * 0.8f);
            Sfx.PlayAt("wood_smash", ground, 1f, 0.5f, 60f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, ground + Vector3.up * 0.2f, Vector3.up, 18, 1.2f, 5f, 60f, new Color(0.42f, 0.43f, 0.5f));
            Strike(ground + Vector3.up, 2.5f, stompDamage, 3f);
            // A ring of crystals rolls out along the floor: only a player on the ground is hit.
            Keep(SpikeRing.Spawn(spikeMesh, crystalMaterial, ground, 1.5f, shockwaveRadius, 9f * Tempo, shockwaveDamage * DamageScale, gameObject, 2.1f, 0.6f, 1.1f));
            yield return Finish();
        }

        // ---------- Phase 3: stage dive ----------

        private IEnumerator StageDive()
        {
            Play("Dive");
            float launch = Moment("launch"), land = Moment("land");
            yield return UntilTime(launch, () => Track(220f));
            Vector3 target = Spot(BossFx.PlayerFeet);
            GroundMarker shadow = Marker(target, diveRadius, true);
            SetAirborne(true);
            Sfx.PlayAt("chain_swish", transform.position, 1f, 0.5f, 50f);
            Vector3 from = transform.position;
            float flight = (land - launch) / Tempo, t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / flight);
                if (t < diveTracking) target = Spot(BossFx.PlayerFeet); // the shadow follows the player, then locks
                shadow.MoveTo(target);
                shadow.Progress = t;
                Vector3 p = Vector3.Lerp(from, target, t);
                p.y += Mathf.Sin(t * Mathf.PI) * 5f;
                transform.position = p;
                Vector3 face = target - from;
                face.y = 0f;
                if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(face);
                yield return null;
            }
            SetAirborne(false);
            Sfx.PlayAt("car_explode", target, 0.8f, 0.7f, 60f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, target + Vector3.up * 0.3f, Vector3.up, 24, 1.5f, 6f, 70f, new Color(0.42f, 0.43f, 0.5f));
            if (Strike(target + Vector3.up, diveRadius, diveDamage, 4f))
            {
                yield return Finish();
                yield break;
            }
            yield return OnBack(); // missed: it belly-flopped and rolled onto its back
        }

        private Vector3 Spot(Vector3 point) => Band != null ? GroundBelow(Band.KeepInZone(point), 4f) : ClampToArena(point, 2f);

        // ---------- Flipped onto its back ----------

        private IEnumerator OnBack()
        {
            LookDuringMove(false);
            Play("FlipOver", 0.08f, false);
            yield return Until("flipped");
            SetFlipped(true);
            Sfx.PlayAt("crab_whimper", transform.position, 0.8f, 1.2f, 40f);
            yield return Finish();
            Play("OnBack", 0.1f, false);
            // On its back until the time is up, or until one hit on its belly has landed.
            float until = Time.time + flipTime;
            while (Time.time < until && !(bellyStruck && Time.frameCount > struckFrame)) yield return null;
            SetFlipped(false); // the belly closes as soon as it starts rolling back over
            Play("FlipUp", 0.1f, false);
            yield return Finish();
        }

        private void SetFlipped(bool on)
        {
            Flipped = on;
            bellyStruck = false;
            if (bodyHitbox != null) bodyHitbox.enabled = !on;
        }

        // ---------- Box-in (driven by BeetleBand) ----------

        /// <summary>Run to a place in the box around the player.</summary>
        public void BeginGather(Vector3 place)
        {
            slot = place;
            Run(BoxIn(), "BoxIn", boxInMove);
        }

        public void UpdateSlot(Vector3 place) => slot = place;

        /// <summary>Stop in place, face the middle and rear up: the warning before the charge.</summary>
        public void BeginBrace()
        {
            if (stage == Stage.Gather) stage = Stage.Brace;
        }

        /// <summary>
        /// Charge the middle of the box, timed to reach it after <paramref name="arriveIn"/> seconds, so the whole band
        /// arrives together, just as the warning ring closes, even if it hadn't quite reached its place.
        /// </summary>
        public void BeginCharge(Vector3 centre, float arriveIn)
        {
            if (stage != Stage.Brace) return;
            chargeAt = centre;
            float distance = Mathf.Max(0f, FlatDistance(centre) - contactDistance);
            currentChargeSpeed = Mathf.Clamp(distance / Mathf.Max(0.1f, arriveIn), chargeSpeed * 0.5f, chargeSpeed * 2.5f);
            stage = Stage.Charge;
        }

        /// <summary>Box-in called off before the charge: chase again at once, but no attack for <paramref name="calm"/> seconds.</summary>
        public void Disperse(float calm)
        {
            if (IsDead) return;
            calmUntil = Mathf.Max(calmUntil, Time.time + calm);
            stage = Stage.None;
        }

        /// <summary>The box-in is over: skid to a stop and get ready to fight again.</summary>
        public void EndFormation() => stage = Stage.None;

        private IEnumerator BoxIn()
        {
            stage = Stage.Gather;
            PassedUnder = Bumped = false;
            Play("Run", 0.15f, false);
            while (stage == Stage.Gather)
            {
                Chase(slot, RunSpeed);
                TurnToward(slot, 540f * Time.deltaTime);
                yield return null;
            }
            if (stage != Stage.Brace) yield break;

            Play("Brace", 0.15f, false);
            while (stage == Stage.Brace)
            {
                Chase(slot, RunSpeed); // a late arrival still gets to its place before the charge
                if (Band != null) TurnToward(Band.FormationCentre, 540f * Time.deltaTime);
                yield return null;
            }
            if (stage != Stage.Charge) yield break;

            Play("Charge", 0.08f, false);
            Halt();
            IgnorePlayer(true); // so a player jumping over doesn't land on its back
            float t = 0f;
            while (stage == Stage.Charge && t < 1.4f)
            {
                t += Time.deltaTime;
                // Committed: straight at the middle of the box, never turning after the player.
                Vector3 direction = FlatDistance(chargeAt) > 0.3f ? FlatDirectionTo(chargeAt) : transform.forward;
                TurnToward(transform.position + direction, 720f * Time.deltaTime);
                if (AgentReady) Agent.Move(direction * currentChargeSpeed * Time.deltaTime);
                if (!PassedUnder && TargetAlive && FlatDistance(Target.position) <= contactDistance) Band?.Contact(this);
                yield return null;
            }
            stage = Stage.None;
            IgnorePlayer(false);
            Play("Idle", 0.2f, false);
            yield return Seconds(0.5f);
        }

        /// <summary>The player was airborne when it arrived: it goes under them.</summary>
        public void PassUnder() => PassedUnder = true;

        /// <summary>Slammed into a bandmate after the player dodged: no damage, but it flips onto its back, belly up.</summary>
        public void Bump(Vector3 away)
        {
            if (Bumped || IsDead) return;
            Bumped = true;
            if (AgentReady) Agent.Move(away.normalized * 1.2f);
            Run(OnBack(), "Flipped", flipMove);
        }

        /// <summary>A lone beetle that missed the jumping player skids and flips over instead.</summary>
        public void Skid()
        {
            if (!IsDead) Run(OnBack(), "Flipped", flipMove);
        }

        public void HitPlayer(float damage, Vector3 direction)
        {
            if (Hurt(damage)) ShovePlayer(direction, 3f);
        }

        private void IgnorePlayer(bool ignore)
        {
            if (ignoringPlayer == ignore || TargetBody == null) return;
            ignoringPlayer = ignore;
            foreach (Collider item in ownColliders)
                if (item != null) Physics.IgnoreCollision(item, TargetBody, ignore);
        }

        // ---------- Mic light and crystals ----------

        protected override void Paint()
        {
            // Green idle, amber gathering, blinking amber (faster and faster) bracing, red charging, blinking blue flipped.
            if (micLight != null)
            {
                Color color = new Color(0.2f, 1f, 0.35f);
                if (stage == Stage.Gather) color = new Color(1f, 0.7f, 0.1f);
                else if (stage == Stage.Brace)
                {
                    float rate = Mathf.Lerp(8f, 30f, Band != null ? Band.BraceProgress : 0f);
                    color = Mathf.Sin(Time.time * rate) > 0f ? new Color(1f, 0.7f, 0.1f) : new Color(0.25f, 0.15f, 0.02f);
                }
                else if (stage == Stage.Charge) color = new Color(1f, 0.1f, 0.05f);
                else if (Flipped) color = Mathf.Sin(Time.time * 6f) > 0f ? new Color(0.2f, 0.3f, 1f) : Color.black;
                micLight.SetColor(BaseColor, color);
                micLight.SetColor(EmissionColor, color * 3f);
            }
            // The shell crystals flare during the box-in and brighten each phase.
            float flare = stage == Stage.Brace ? 1f + 2f * (Band != null ? Band.BraceProgress : 0f) : stage == Stage.Charge ? 3f : 1f;
            flare *= 1f + 0.4f * (DuelPhase - 1);
            for (int i = 0; i < crystals.Length; i++) crystals[i].SetColor(EmissionColor, crystalGlow[i] * flare);
        }
    }
}
