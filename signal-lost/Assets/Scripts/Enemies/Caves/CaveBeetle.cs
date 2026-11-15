using SignalLost.Enemies.Desert;
using UnityEngine;

namespace SignalLost.Enemies.Caves
{
    /// <summary>
    /// One of the four Crystal Caves beetle mini-bosses (Bug Lennon, Paul Arthropodney, George Harrison and Scarab
    /// Starr): would-be rock stars with microphone packs on their backs and little headset mics. They fight as a band,
    /// coordinated by <see cref="BeetleBand"/>, and never leave their fight zone.
    ///
    /// On its own, a beetle circles the player and, when the band gives it the turn, rears up and head-butts. Together
    /// they do the <b>box-in</b>: they surround the player, rear up with their mic lights blinking amber while a ring
    /// closes around the player, then all charge at once when the lights go red. Grounded when they arrive: the player
    /// takes the hit and the beetles don't touch each other. In the air (a jump timed to the charge): they pass under
    /// and slam into each other, hurting and dazing themselves.
    ///
    /// No phase 2 of its own: the band gets angrier as members fall (faster, more frequent box-ins). It drops scrap
    /// (ScrapDropper) and no ship part. Built on <see cref="DesertMiniBoss"/> for the patrol, the leash
    /// and reset, and death. All numbers are placeholders.
    /// </summary>
    public sealed class CaveBeetle : DesertMiniBoss
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private enum Mode { Fight, WindUp, Lunge, Gather, Brace, Charge, Dizzy, Recover }

        [Header("Voice")]
        [Tooltip("Said in order, one per hit (when no one else is talking), so the band's story carries on through the fight.")]
        [SerializeField] private string[] hitLines = { "Ow!" };
        [SerializeField] private string lastWords = "Ow.";

        [Header("Moves")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 3.6f;
        [Tooltip("Distance it circles the player at while it isn't its turn to attack.")]
        [SerializeField, Min(1f)] private float orbitRadius = 6.5f;
        [SerializeField, Min(0.5f)] private float headbuttRange = 2.4f;
        [Tooltip("Rearing back before the head-butt: the warning.")]
        [SerializeField, Min(0.1f)] private float windUpTime = 0.55f;
        [SerializeField, Min(0f)] private float headbuttDamage = 12f;
        [SerializeField, Min(0f)] private float headbuttCooldown = 1.4f;

        [Header("Box-in charge")]
        [SerializeField, Min(1f)] private float chargeSpeed = 14f;
        [Tooltip("Flat distance from the player at which a charging beetle hits (or passes under) them.")]
        [SerializeField, Min(0.5f)] private float contactDistance = 1.7f;
        [Tooltip("Damage each beetle takes when they slam into each other after a dodge.")]
        [SerializeField, Min(0f)] private float bumpDamage = 30f;
        [SerializeField, Min(0f)] private float dizzyTime = 2f;

        [Header("Rig")]
        [Tooltip("The shell and head; rears back for the head-butt and the box-in.")]
        [SerializeField] private Transform body;
        [SerializeField] private Transform[] legs = new Transform[0];
        [Tooltip("The microphone pack's light: green idle, blinking amber while bracing, red on the charge.")]
        [SerializeField] private Renderer micLight;
        [Tooltip("The crystal growths on its shell; they flare during the box-in.")]
        [SerializeField] private Renderer[] crystals = new Renderer[0];

        public BeetleBand Band { get; set; }
        public string[] HitLines => hitLines;
        public string LastWords => lastWords;
        public float ChargeSpeed => chargeSpeed;
        public float ContactDistance => contactDistance;
        public bool InFormation => mode == Mode.Gather || mode == Mode.Brace || mode == Mode.Charge;
        /// <summary>Free to be called into a box-in (not dazed, not in the middle of one).</summary>
        public bool ReadyForFormation => !IsDead && Engaged && !InPhaseShift && (mode == Mode.Fight || mode == Mode.WindUp);
        public bool Charging => mode == Mode.Charge;
        public bool PassedUnder { get; private set; }
        public bool Bumped { get; private set; }

        private Mode mode = Mode.Fight;
        private float modeTime, nextAttack, nextRepath, orbitAngle, currentChargeSpeed;
        private Vector3 chargeAt;
        private Vector3 slot, lungeDirection;
        private bool lungeHit;
        private Quaternion bodyRest = Quaternion.identity;
        private Vector3 bodyRestPosition;
        private MaterialPropertyBlock block;
        private Color[] crystalGlow = new Color[0];
        private Collider[] ownColliders = new Collider[0];
        private bool ignoringPlayer;

        protected override void Awake()
        {
            base.Awake();
            block = new MaterialPropertyBlock();
            if (body != null)
            {
                bodyRest = body.localRotation;
                bodyRestPosition = body.localPosition;
            }
            crystalGlow = new Color[crystals.Length];
            for (int i = 0; i < crystals.Length; i++)
                crystalGlow[i] = crystals[i] != null && crystals[i].sharedMaterial.HasProperty(EmissionColor) ? crystals[i].sharedMaterial.GetColor(EmissionColor) : Color.black;
            ownColliders = GetComponentsInChildren<Collider>(true);
            orbitAngle = Random.value * 360f;
        }

        // ---------- Mini-boss hooks ----------

        public override bool HasPhaseTwo => false; // the band gets angrier instead (BeetleBand)
        public override bool ShowsBossBar => false; // four bars would cover the screen; aim at one to see its name and health

        /// <summary>Joins the fight when a bandmate does.</summary>
        public void JoinFight() => Engage();

        protected override void OnEngaged() => Band?.MemberEngaged(this);

        protected override void TickCombat(float dt)
        {
            modeTime += dt;
            UpdateShove(dt);
            switch (mode)
            {
                case Mode.Fight: UpdateFight(); break;
                case Mode.WindUp: UpdateWindUp(); break;
                case Mode.Lunge: UpdateLunge(dt); break;
                case Mode.Gather:
                    MoveTo(slot, moveSpeed * 2f);
                    break;
                case Mode.Brace:
                    MoveTo(slot, moveSpeed * 1.5f); // keeps its place in the box as the player moves
                    if (Band != null) TurnToward(Band.FormationCentre, 540f * dt);
                    break;
                case Mode.Charge: UpdateCharge(dt); break;
                case Mode.Dizzy:
                    Halt();
                    if (modeTime >= dizzyTime) Enter(Mode.Recover);
                    break;
                case Mode.Recover:
                    Halt();
                    if (Target != null) TurnToward(Target.position, 180f * dt);
                    if (modeTime >= 0.6f) Enter(Mode.Fight);
                    break;
            }
        }

        private void Enter(Mode next)
        {
            if (mode == Mode.WindUp || mode == Mode.Lunge) Band?.ReleaseTurn(this);
            mode = next;
            modeTime = 0f;
            if (next != Mode.Charge && next != Mode.Dizzy && next != Mode.Recover) IgnorePlayer(false);
        }

        private float Anger => Band != null ? Band.SpeedScale : 1f;

        private void UpdateFight()
        {
            if (!TargetAlive) { Halt(); return; }
            float distance = FlatDistance(Target.position);
            if (Time.time >= nextAttack && (Band == null || Band.TryTakeTurn(this)))
            {
                if (distance <= headbuttRange && AngleToTarget() < 35f)
                {
                    Halt();
                    Enter(Mode.WindUp);
                    return;
                }
                if (Time.time >= nextRepath) { nextRepath = Time.time + 0.2f; MoveTo(Target.position, moveSpeed * Anger); }
                if (distance <= headbuttRange + 1f) TurnToward(Target.position, 360f * Time.deltaTime);
                return;
            }
            // Not its turn: circle the player at a distance, like a band member waiting for their solo.
            orbitAngle += 25f * Time.deltaTime;
            Vector3 around = Quaternion.Euler(0f, orbitAngle, 0f) * Vector3.forward * orbitRadius;
            if (Time.time >= nextRepath) { nextRepath = Time.time + 0.3f; MoveTo(Target.position + around, moveSpeed * Anger); }
            if (distance < orbitRadius + 2f) TurnToward(Target.position, 240f * Time.deltaTime);
        }

        private void UpdateWindUp()
        {
            Halt();
            if (Target != null) TurnToward(Target.position, 240f * Time.deltaTime);
            if (modeTime < windUpTime / Anger) return;
            lungeDirection = Target != null ? FlatDirectionTo(Target.position) : transform.forward;
            lungeHit = false;
            Enter(Mode.Lunge);
        }

        private void UpdateLunge(float dt)
        {
            if (AgentReady) Agent.Move(lungeDirection * 10f * dt);
            if (!lungeHit && TargetAlive && FlatDistance(Target.position) <= headbuttRange + 0.3f &&
                Vector3.Angle(lungeDirection, FlatDirectionTo(Target.position)) < 60f)
            {
                lungeHit = true;
                if (HurtPlayer(headbuttDamage)) ShovePlayer(lungeDirection, 2f);
            }
            if (modeTime < 0.25f) return;
            nextAttack = Time.time + headbuttCooldown / Anger;
            Enter(Mode.Fight);
        }

        // ---------- Box-in (driven by BeetleBand) ----------

        /// <summary>Run to a place in the box around the player.</summary>
        public void BeginGather(Vector3 place)
        {
            slot = Band != null ? Band.KeepInZone(place) : place;
            PassedUnder = Bumped = false;
            Enter(Mode.Gather);
        }

        public void UpdateSlot(Vector3 place) => slot = Band != null ? Band.KeepInZone(place) : place;

        /// <summary>Stop in place, face the player and rear up: the warning before the charge.</summary>
        public void BeginBrace() => Enter(Mode.Brace);

        /// <summary>
        /// Charge at the player (straight at them, tracking, on the NavMesh), timed to reach them after
        /// <paramref name="arriveIn"/> seconds, so the whole band arrives together, just as the warning ring closes,
        /// even if it hadn't quite reached its place in the box.
        /// </summary>
        public void BeginCharge(Vector3 centre, float arriveIn)
        {
            PassedUnder = Bumped = false;
            chargeAt = centre;
            float distance = Mathf.Max(0f, FlatDistance(centre) - contactDistance);
            currentChargeSpeed = Mathf.Clamp(distance / Mathf.Max(0.1f, arriveIn), chargeSpeed * 0.5f, chargeSpeed * 2.5f);
            IgnorePlayer(true); // so a player jumping over doesn't land on its back
            Enter(Mode.Charge);
        }

        /// <summary>Box-in called off before the charge: chase again at once, but no attack for <paramref name="calm"/> seconds.</summary>
        public void Disperse(float calm)
        {
            if (IsDead) return;
            nextAttack = Mathf.Max(nextAttack, Time.time + calm);
            Enter(Mode.Fight);
        }

        /// <summary>The box-in is over: skid to a stop and get ready to fight again.</summary>
        public void EndFormation()
        {
            if (mode == Mode.Dizzy || IsDead) return;
            Enter(Mode.Recover);
        }

        private void UpdateCharge(float dt)
        {
            if (Band == null) { Enter(Mode.Recover); return; }
            // Committed: straight at the middle of the box, never turning after the player. Past the middle it keeps going.
            Vector3 direction = FlatDistance(chargeAt) > 0.3f ? FlatDirectionTo(chargeAt) : transform.forward;
            TurnToward(transform.position + direction, 720f * dt);
            if (AgentReady) Agent.Move(direction * currentChargeSpeed * dt);
            if (!PassedUnder && TargetAlive && FlatDistance(Target.position) <= contactDistance) Band.Contact(this);
            if (modeTime > 1.4f) Enter(Mode.Recover); // never charge forever
        }

        /// <summary>The player was airborne when it arrived: it goes under them.</summary>
        public void PassUnder() => PassedUnder = true;

        /// <summary>Slammed into a bandmate after the player dodged: hurt (credited to the player) and dazed.</summary>
        public void Bump(Vector3 away, GameObject credit)
        {
            if (Bumped || IsDead) return;
            Bumped = true;
            if (AgentReady) Agent.Move(away.normalized * 1.2f);
            Enter(Mode.Dizzy);
            Health.TakeDamage(bumpDamage, credit);
        }

        /// <summary>A lone beetle that missed the jumping player skids into a daze instead.</summary>
        public void Skid() => Enter(Mode.Dizzy);

        public void HitPlayer(float damage, Vector3 direction)
        {
            if (HurtPlayer(damage)) ShovePlayer(direction, 3f);
        }

        private void IgnorePlayer(bool ignore)
        {
            if (ignoringPlayer == ignore || TargetBody == null) return;
            ignoringPlayer = ignore;
            foreach (Collider item in ownColliders)
                if (item != null) Physics.IgnoreCollision(item, TargetBody, ignore);
        }

        /// <summary>Walks somewhere, never leaving the band's fight zone.</summary>
        private void MoveTo(Vector3 point, float speed) => Chase(Band != null ? Band.KeepInZone(point) : point, speed);

        // ---------- Talking, hits, reset, death ----------

        protected override void OnBossHit(float amount, GameObject source, Vector3 away) => Band?.MemberHit(this);

        protected override void OnPhaseTwo() { }

        protected override void OnReset()
        {
            mode = Mode.Fight;
            modeTime = 0f;
            IgnorePlayer(false);
            Band?.ReleaseTurn(this);
            Band?.MemberReset(this);
        }

        protected override void OnBossDeath()
        {
            IgnorePlayer(false);
            Band?.ReleaseTurn(this);
            Band?.MemberDown(this);
        }

        // ---------- Animation ----------

        protected override void AnimateBoss(float dt)
        {
            float speed = AgentReady ? Agent.velocity.magnitude : 0f;
            if (mode == Mode.Charge) speed = currentChargeSpeed;
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                float swing = Mathf.Sin(Time.time * (5f + speed * 2.5f) + i * 2.1f) * Mathf.Clamp(speed * 8f, 0f, 22f);
                legs[i].localRotation = Quaternion.Euler(0f, swing, 0f);
            }

            float rear = 0f, wobble = 0f;
            if (mode == Mode.WindUp) rear = Mathf.Clamp01(modeTime / (windUpTime / Anger)) * 14f;
            else if (mode == Mode.Brace) rear = 22f + Mathf.Sin(Time.time * 18f) * 3f; // rocking on its back legs
            else if (mode == Mode.Lunge || mode == Mode.Charge) rear = -8f;
            if (mode == Mode.Dizzy) wobble = Mathf.Sin(Time.time * 9f) * 12f;
            float bounce = Engaged && mode == Mode.Fight ? Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.04f : 0f; // nodding along to a beat
            if (body != null)
            {
                body.localRotation = bodyRest * Quaternion.Euler(-rear, 0f, wobble);
                body.localPosition = bodyRestPosition + Vector3.up * bounce;
            }
            if (mode == Mode.Dizzy) Visual.localRotation = VisualRestRotation * Quaternion.Euler(0f, Mathf.Sin(Time.time * 5f) * 25f, 0f);
            else Visual.localRotation = VisualRestRotation;
        }

        protected override void Paint()
        {
            // Mic light: green idle, blinking amber (faster and faster) while bracing, solid red on the charge.
            if (micLight != null)
            {
                Color color = new Color(0.2f, 1f, 0.35f);
                if (mode == Mode.Gather) color = new Color(1f, 0.7f, 0.1f);
                else if (mode == Mode.Brace)
                {
                    float rate = Mathf.Lerp(8f, 30f, Band != null ? Band.BraceProgress : 0f);
                    color = Mathf.Sin(Time.time * rate) > 0f ? new Color(1f, 0.7f, 0.1f) : new Color(0.25f, 0.15f, 0.02f);
                }
                else if (mode == Mode.Charge) color = new Color(1f, 0.1f, 0.05f);
                else if (mode == Mode.Dizzy) color = Mathf.Sin(Time.time * 6f) > 0f ? new Color(0.2f, 0.3f, 1f) : Color.black;
                micLight.GetPropertyBlock(block);
                block.SetColor(BaseColor, color);
                block.SetColor(EmissionColor, color * 3f);
                micLight.SetPropertyBlock(block);
            }
            // Shell crystals flare during the box-in.
            float flare = mode == Mode.Brace ? 1f + 2f * (Band != null ? Band.BraceProgress : 0f) : mode == Mode.Charge ? 3f : 1f;
            for (int i = 0; i < crystals.Length; i++)
            {
                if (crystals[i] == null) continue;
                if (flare <= 1.001f) { crystals[i].SetPropertyBlock(null); continue; }
                crystals[i].GetPropertyBlock(block);
                block.SetColor(EmissionColor, crystalGlow[i] * flare);
                crystals[i].SetPropertyBlock(block);
            }
        }
    }
}
