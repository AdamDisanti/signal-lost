using System;
using SignalLost.Effects;
using SignalLost.Enemies.Desert;
using SignalLost.Scanner;
using UnityEngine;

namespace SignalLost.Enemies.Caves
{
    /// <summary>
    /// Crystal Caves fodder: a chameleon whose back is a cluster of the cave's own crystals. Camouflaged, it lies
    /// buried with only those crystals showing, so it looks exactly like any other crystal batch: no colliders, no
    /// scanner signal, no AI. <see cref="Disrupt"/> (meant for the scanner's future camouflage-disrupting upgrade)
    /// turns its crystals red and it rises out of the ground through the shared <see cref="BurrowEmerge"/>; after the
    /// emerge grace it fights. Its crystals stay red while it is active and go dark when it dies.
    ///
    /// It is a ranged enemy: it keeps a few metres away and flicks its tongue at the player. The wind-up (head
    /// pulls back, mouth opens, throat swells, crystals pulse) is the warning; the aim locks shortly before the
    /// tongue shoots out in a straight line, so a sidestep dodges it. Walls stop the tongue.
    ///
    /// Builds on <see cref="DesertFodder"/> like every other small enemy (territory, leash and heal, hit flash,
    /// combo stun, death). Without a BurrowEmerge component it simply skips the rise. All numbers are placeholders.
    /// </summary>
    public sealed class CrystalChameleon : DesertFodder, IBurrowOffset
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private enum State { Camouflaged, Emerging, Stalk, WindUp, Lick, Recover }

        [Header("Camouflage")]
        [Tooltip("The cave crystal material it copies (Prefabs/Enviornment objects/CrystalCaves/Materials). Match the batches around it.")]
        [SerializeField] private Material crystalMaterial;
        [Tooltip("Its crystals once disrupted and while it is active.")]
        [SerializeField] private Material disruptedMaterial;
        [Tooltip("Its crystals after it dies (switched off).")]
        [SerializeField] private Material spentMaterial;
        [SerializeField] private Renderer[] crystals = new Renderer[0];
        [Tooltip("Its crystal glow, like a real batch's light.")]
        [SerializeField] private Light glow;
        [Tooltip("Test scenes: reveal straight away on Start instead of waiting to be disrupted.")]
        [SerializeField] private bool startRevealed;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 3.2f;
        [Tooltip("It closes in until the player is this close (and visible), then stops to attack.")]
        [SerializeField, Min(1f)] private float preferredRange = 5.5f;
        [Tooltip("Closer than this, it backs away.")]
        [SerializeField, Min(0.5f)] private float tooCloseRange = 3f;

        [Header("Tongue")]
        [SerializeField, Min(1f)] private float tongueRange = 8f;
        [Tooltip("The warning before the tongue shoots out.")]
        [SerializeField, Min(0.1f)] private float windUpTime = 0.65f;
        [Tooltip("Fraction of the wind-up after which the aim stops following the player.")]
        [SerializeField, Range(0f, 1f)] private float aimLockAt = 0.75f;
        [SerializeField, Min(0.02f)] private float extendTime = 0.12f;
        [SerializeField, Min(0f)] private float holdTime = 0.1f;
        [SerializeField, Min(0.02f)] private float retractTime = 0.3f;
        [SerializeField, Min(0f)] private float tongueDamage = 10f;
        [Tooltip("How close the tongue has to pass to the player's body to hit.")]
        [SerializeField, Min(0.05f)] private float tongueHitRadius = 0.3f;
        [SerializeField, Min(0f)] private float attackCooldown = 1.6f;

        [Header("Rig")]
        [SerializeField] private Transform head;
        [SerializeField] private Transform jaw;
        [SerializeField] private Transform throat;
        [Tooltip("The tongue's start, inside the mouth. Its forward is ignored; the tongue aims at the player.")]
        [SerializeField] private Transform mouth;
        [Tooltip("A unit-length +Z mesh, stretched to the tongue's length.")]
        [SerializeField] private Transform tongue;
        [SerializeField] private Transform tongueTip;
        [SerializeField] private Transform[] eyes = new Transform[0];
        [SerializeField] private Transform[] legs = new Transform[0];
        [SerializeField] private Transform tail;

        /// <summary>Raised once, when it is disrupted.</summary>
        public event Action<CrystalChameleon> Revealed;

        public bool IsCamouflaged => state == State.Camouflaged;
        public Material CrystalMaterial => crystalMaterial;

        /// <summary>IBurrowOffset: BurrowEmerge's rise, added to the visual's height.</summary>
        public float BurrowOffset { get; set; }

        private State state = State.Camouflaged;
        private BurrowEmerge burrow;
        private EnemySignalSource signal;
        private float stateTime, nextAttack, nextRepath, tongueLength, tongueReach;
        private bool tongueHit;
        private Vector3 aim;
        private Vector3 headRest, throatRest, tailRest;
        private Quaternion jawRest = Quaternion.identity;
        private Quaternion[] eyeRest = new Quaternion[0];
        private Renderer[] tongueRenderers = new Renderer[0];
        private MaterialPropertyBlock crystalBlock;
        private Color disruptedGlow = Color.red;
        private float glowIntensity;

        private void OnValidate() => ApplyCrystalMaterial(crystalMaterial);

        protected override void Awake()
        {
            base.Awake();
            burrow = GetComponent<BurrowEmerge>();
            signal = GetComponent<EnemySignalSource>();
            if (signal != null) signal.enabled = false; // hidden from the ordinary scanner until disrupted
            crystalBlock = new MaterialPropertyBlock();
            if (head != null) headRest = head.localPosition;
            if (throat != null) throatRest = throat.localScale;
            if (jaw != null) jawRest = jaw.localRotation;
            if (tail != null) tailRest = tail.localEulerAngles;
            eyeRest = new Quaternion[eyes.Length];
            for (int i = 0; i < eyes.Length; i++) eyeRest[i] = eyes[i] != null ? eyes[i].localRotation : Quaternion.identity;
            if (tongue != null) tongueRenderers = tongue.GetComponentsInChildren<Renderer>(true);
            if (glow != null) glowIntensity = glow.intensity;
            if (disruptedMaterial != null) disruptedGlow = CrystalBatch.GlowColor(disruptedMaterial);
            ApplyCrystalMaterial(crystalMaterial);
            SetTongue(0f);
        }

        protected override void OnStarted()
        {
            Halt();
            if (startRevealed) Disrupt();
        }

        /// <summary>
        /// Breaks its camouflage: the crystals turn red and it rises out of the ground, then attacks. This is the hook
        /// for the scanner's camouflage-disrupting upgrade. Does nothing once it is already revealed or dead.
        /// </summary>
        [ContextMenu("Disrupt")] // testing until the scanner upgrade exists
        public void Disrupt()
        {
            if (state != State.Camouflaged || IsDead) return;
            ApplyCrystalMaterial(disruptedMaterial);
            if (glow != null) glow.color = disruptedGlow;
            if (signal != null) signal.enabled = true;
            LastSeen = Time.time;
            Enter(State.Emerging);
            if (burrow != null) burrow.Emerge();
            Revealed?.Invoke(this);
        }

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
        }

        protected override void Tick(float dt)
        {
            stateTime += dt;
            switch (state)
            {
                case State.Camouflaged:
                    Halt();
                    break;
                case State.Emerging:
                    Halt();
                    if (burrow == null || !burrow.IsBuried) Enter(State.Stalk);
                    break;
                case State.Stalk: UpdateStalk(); break;
                case State.WindUp: UpdateWindUp(); break;
                case State.Lick: UpdateLick(dt); break;
                case State.Recover:
                    Halt();
                    if (Target != null) TurnToward(Target.position, 240f * dt);
                    if (stateTime >= 0.35f) Enter(State.Stalk);
                    break;
            }
            Animate(dt);
        }

        private bool AllowedToAttack => burrow == null || burrow.CanAttack;

        private void UpdateStalk()
        {
            if (!TargetAlive) { Halt(); return; }
            if (Notices()) LastSeen = Time.time;
            if (Time.time - LastSeen > 4f) { Halt(); return; } // lost interest; waits where it is
            if (GaveUp()) return;

            float distance = FlatDistance(Target.position);
            bool visible = CanSee(tongueRange);
            if (visible) LastSeen = Time.time;
            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + 0.25f;
                if (!visible || distance > preferredRange) Chase(Target.position, moveSpeed);
                else if (distance < tooCloseRange) Chase(transform.position - FlatDirectionTo(Target.position) * 3f, moveSpeed);
                else Halt();
            }
            if (distance <= preferredRange + 0.5f) TurnToward(Target.position, 300f * Time.deltaTime);

            if (visible && distance <= tongueRange && Time.time >= nextAttack && AllowedToAttack &&
                Vector3.Angle(transform.forward, FlatDirectionTo(Target.position)) < 30f)
            {
                Halt();
                Enter(State.WindUp);
            }
        }

        private Vector3 TargetChest => Target.position + Vector3.up * 1.1f;
        private Vector3 MouthPoint => mouth != null ? mouth.position : transform.position + Vector3.up * 0.4f + transform.forward * 0.8f;

        private void UpdateWindUp()
        {
            Halt();
            if (!TargetAlive) { Enter(State.Stalk); return; }
            float k = stateTime / windUpTime;
            if (k < aimLockAt)
            {
                TurnToward(Target.position, 360f * Time.deltaTime);
                aim = (TargetChest - MouthPoint).normalized;
            }
            if (stateTime < windUpTime) return;
            if (!AllowedToAttack) { Enter(State.Stalk); return; }
            tongueReach = ClearReach(aim);
            tongueHit = false;
            Enter(State.Lick);
        }

        /// <summary>How far the tongue can go along a direction before scenery stops it.</summary>
        private float ClearReach(Vector3 direction)
        {
            float reach = tongueRange;
            RaycastHit[] hits = Physics.RaycastAll(MouthPoint, direction, tongueRange, sightMask, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                Transform other = hit.collider.transform;
                if (other.IsChildOf(transform) || (Target != null && other.IsChildOf(Target))) continue;
                if (hit.collider.GetComponentInParent<SignalLost.Combat.Health>() != null) continue; // creatures don't stop it
                reach = Mathf.Min(reach, hit.distance);
            }
            return reach;
        }

        private void UpdateLick(float dt)
        {
            Halt();
            float t = stateTime;
            if (t < extendTime) tongueLength = tongueReach * (t / extendTime);
            else if (t < extendTime + holdTime) tongueLength = tongueReach;
            else tongueLength = tongueReach * Mathf.Clamp01(1f - (t - extendTime - holdTime) / retractTime);

            if (!tongueHit && t <= extendTime + holdTime && TongueTouchesTarget())
            {
                tongueHit = true;
                if (TargetHealth != null && TargetAlive) TargetHealth.TakeDamage(tongueDamage, gameObject);
            }

            if (t >= extendTime + holdTime + retractTime)
            {
                tongueLength = 0f;
                nextAttack = Time.time + attackCooldown;
                Enter(State.Recover);
            }
        }

        // Closest approach between the tongue and the player's body (a vertical line from knees to head).
        private bool TongueTouchesTarget()
        {
            if (!TargetAlive) return false;
            float radius = tongueHitRadius + (TargetBody != null ? TargetBody.radius : 0.4f);
            Vector3 start = MouthPoint, end = start + aim * tongueLength;
            float height = TargetBody != null ? TargetBody.height : 1.8f;
            for (float h = 0.3f; h <= height; h += 0.3f)
            {
                Vector3 body = Target.position + Vector3.up * h;
                if (DistanceToSegment(body, start, end) <= radius) return true;
            }
            return false;
        }

        private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(point, a + ab * t);
        }

        protected override void OnHit(float amount, GameObject source, Vector3 away)
        {
            if (state == State.Stalk) nextRepath = 0f; // react straight away
        }

        protected override void OnDeath()
        {
            tongueLength = 0f;
            SetTongue(0f);
            ApplyCrystalMaterial(spentMaterial); // the crystals switch off
            if (glow != null) glow.enabled = false;
        }

        // ---------- Look ----------

        private void ApplyCrystalMaterial(Material material)
        {
            if (material == null) return;
            foreach (Renderer crystal in crystals)
                if (crystal != null) crystal.sharedMaterial = material;
            if (glow != null && material != spentMaterial) glow.color = CrystalBatch.GlowColor(material);
        }

        protected override void Paint()
        {
            // Crystals pulse brighter during the wind-up: part of the tongue warning.
            bool pulsing = state == State.WindUp && disruptedMaterial != null;
            float k = pulsing ? Mathf.Clamp01(stateTime / windUpTime) : 0f;
            foreach (Renderer crystal in crystals)
            {
                if (crystal == null) continue;
                if (!pulsing)
                {
                    crystal.SetPropertyBlock(null);
                    continue;
                }
                crystal.GetPropertyBlock(crystalBlock);
                Color emission = disruptedMaterial.GetColor(EmissionColor);
                crystalBlock.SetColor(EmissionColor, emission * (1f + 2.5f * k * (0.6f + 0.4f * Mathf.Sin(Time.time * 40f))));
                crystal.SetPropertyBlock(crystalBlock);
            }
            if (glow != null && glow.enabled) glow.intensity = glowIntensity * (1f + 1.5f * k);
        }

        private void SetTongue(float length)
        {
            bool show = length > 0.02f;
            foreach (Renderer item in tongueRenderers)
                if (item != null) item.enabled = show;
            if (tongueTip != null) tongueTip.gameObject.SetActive(show);
            if (!show || tongue == null) return;
            Vector3 start = MouthPoint;
            tongue.position = start;
            tongue.rotation = Quaternion.LookRotation(aim);
            Vector3 scale = tongue.localScale;
            Vector3 parentScale = tongue.parent != null ? tongue.parent.lossyScale : Vector3.one;
            tongue.localScale = new Vector3(scale.x, scale.y, length / Mathf.Max(0.001f, parentScale.z));
            if (tongueTip != null) tongueTip.position = start + aim * length;
        }

        private void Animate(float dt)
        {
            float speed = AgentReady ? Agent.velocity.magnitude : 0f;

            // Slow, deliberate chameleon gait: legs swing in diagonal pairs, the body sways.
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                float phase = (i == 0 || i == 3) ? 0f : Mathf.PI;
                float swing = Mathf.Sin(Time.time * (3f + speed * 3f) + phase) * Mathf.Clamp(speed * 12f, 0f, 28f);
                legs[i].localRotation = Quaternion.Euler(0f, swing, 0f);
            }

            float windUp = state == State.WindUp ? Mathf.Clamp01(stateTime / windUpTime) : 0f;
            float lick = state == State.Lick ? 1f : 0f;
            float sway = Mathf.Sin(Time.time * 4f) * Mathf.Clamp01(speed) * 4f;
            Vector3 shake = state == State.Emerging ? UnityEngine.Random.insideUnitSphere * 0.02f : Vector3.zero;
            Visual.localPosition = VisualRest + Vector3.up * BurrowOffset + shake;
            Visual.localRotation = VisualRestRotation * Quaternion.Euler(0f, 0f, sway);

            if (head != null) head.localPosition = headRest + Vector3.back * (0.08f * windUp) + Vector3.forward * (0.05f * lick);
            if (jaw != null) jaw.localRotation = jawRest * Quaternion.Euler(28f * Mathf.Max(windUp, lick), 0f, 0f);
            if (throat != null) throat.localScale = throatRest * (1f + 0.6f * windUp * (0.85f + 0.15f * Mathf.Sin(Time.time * 30f)));
            if (tail != null) tail.localEulerAngles = tailRest + new Vector3(0f, Mathf.Sin(Time.time * 1.3f) * 6f, 0f);

            // Each eye turret swivels on its own: wandering while idle, locked on the player once revealed.
            for (int i = 0; i < eyes.Length; i++)
            {
                if (eyes[i] == null) continue;
                Quaternion want = eyeRest[i] * Quaternion.Euler(Mathf.Sin(Time.time * 0.7f + i * 2f) * 20f, Mathf.Sin(Time.time * 0.9f + i * 3f) * 35f, 0f);
                if (state != State.Camouflaged && Target != null && eyes[i].parent != null)
                {
                    Vector3 look = TargetChest - eyes[i].position;
                    if (look.sqrMagnitude > 0.01f)
                        want = Quaternion.Inverse(eyes[i].parent.rotation) * Quaternion.LookRotation(look);
                }
                eyes[i].localRotation = Quaternion.Slerp(eyes[i].localRotation, want, 10f * dt);
            }
            SetTongue(tongueLength);
        }
    }
}
