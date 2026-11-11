using System.Collections.Generic;
using System.Linq;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Core;
using SignalLost.Effects;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace SignalLost.Melee
{
    /// <summary>
    /// Dedicated melee (Adam, 11-10; docs/melee-rage.md): Q cycles the laser, the scanner and bare fists
    /// (<see cref="PlayerGear"/>). Left click throws a three-punch combo (jab, cross, a heavy hook that shoves and
    /// jolts small enemies). Gaussian Brass Knuckles make the punches hit harder and the player faster with fists up,
    /// and unlock a tier at a time:
    /// I   magnet lunge: punching an enemy in your sights up to <see cref="lungeRange"/> m away flings you onto it, so you
    ///     can bounce from enemy to enemy;
    /// II  overcharge marks, chain lightning and the magnet pull: punches mark enemies; punching a marked enemy or killing
    ///     one with your fists throws chain lightning (<see cref="ChainArcs"/>); right click yanks a small enemy to you and
    ///     pounds it (big ones and bosses pull you to them instead);
    /// III finishers: a staggered small enemy (low on health) glows; V slams it: a guaranteed kill, a shockwave, chain
    ///     lightning, and every overcharge mark nearby goes off. You can't be hurt during a finisher.
    /// Kills feed <see cref="RageMeter"/>, which makes all of this reach further.
    ///
    /// Added to the player at runtime by <see cref="PlayerGear"/> and switched on only while the fists are out (like the
    /// laser). The hands are posed by <see cref="FirstPersonHands"/> from <see cref="GetPose"/>. Numbers are provisional.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFists : MonoBehaviour
    {
        [Header("Punches (placeholder values)")]
        [Tooltip("Damage of each punch in order: jab, cross, hook.")]
        [SerializeField] private float[] comboDamage = { 9f, 9f, 16f };
        [SerializeField] private float[] comboDuration = { 0.26f, 0.26f, 0.42f };
        [SerializeField, Min(0.5f)] private float range = 2.3f;
        [SerializeField, Range(10f, 180f)] private float arc = 60f;
        [SerializeField, Min(0f)] private float comboWindow = 0.35f;
        [SerializeField, Min(0f)] private float hitStop = 0.05f;

        [Header("Knuckles I: magnet lunge")]
        [SerializeField, Min(1f)] private float lungeRange = 9f;
        [SerializeField, Min(1f)] private float lungeSpeed = 26f;
        [Tooltip("Degrees off the crosshair an enemy may be and still be lunged at.")]
        [SerializeField, Min(1f)] private float lungeCone = 10f;

        [Header("Knuckles II: marks, chain lightning, magnet pull")]
        [SerializeField, Min(1)] private int chainJumps = 3;
        [SerializeField, Min(1f)] private float chainReach = 7f;
        [SerializeField, Min(0f)] private float chainDamage = 14f;
        [SerializeField, Min(2f)] private float pullRange = 20f;
        [SerializeField, Min(1f)] private float pullCone = 7f;
        [SerializeField, Min(0f)] private float pullCooldown = 2.5f;
        [SerializeField, Min(0f)] private float poundDamage = 30f;

        [Header("Knuckles III: finishers")]
        [Tooltip("A small enemy can be finished at or below this share of its health.")]
        [SerializeField, Range(0.05f, 1f)] private float finishBelow = 0.35f;
        [SerializeField, Min(1f)] private float finishRange = 6f;
        [SerializeField, Min(1f)] private float shockRadius = 5f;
        [SerializeField, Min(0f)] private float shockDamage = 45f;
        [SerializeField, Min(0f)] private float markBurstDamage = 30f;
        [SerializeField, Min(0.5f)] private float markBurstReach = 3.2f;

        private enum Move { None, Punch, Pound, Finisher }

        private InputActionAsset runtimeActions;
        private InputAction attack, pull;
        private Transform eye;
        private Camera view;
        private FirstPersonController movement;
        private Health health;
        private RageMeter rage;
        private PlayerGear gear;

        private Move move;
        private int comboStep;
        private float elapsed, length, hitAt, hitStopUntil, lastPunchEnd = float.NegativeInfinity;
        private bool hitApplied, queued, waitForRelease;
        private Health lungeTarget, moveTarget;
        private bool invulnerableBefore, madeInvulnerable;

        private Health pullTarget;
        private float pullStarted, pullReadyAt;
        private LineRenderer pullBeam;

        private readonly List<Health> struck = new List<Health>();
        private readonly List<Vector3> struckPoints = new List<Vector3>();
        private readonly Collider[] overlaps = new Collider[48];
        private readonly List<Health> nearby = new List<Health>();
        private readonly Dictionary<Health, FinisherGlow> glows = new Dictionary<Health, FinisherGlow>();
        private float nextGlowScan;

        private Vector3 kick;
        private float fovKick;
        private Quaternion unkicked;
        private float unkickedFov;
        private bool kickApplied;

        private (List<GameObject> parts, List<Renderer> glowing) rightKnuckles, leftKnuckles;
        private readonly List<Material> glowMaterials = new List<Material>();
        private float glowFlash;

        /// <summary>0 bare fists; 1-3 the Gaussian Brass Knuckles tier (set by PlayerUpgrades).</summary>
        public int KnuckleTier { get; set; }
        /// <summary>Punch damage multiplier from the knuckles (set by PlayerUpgrades).</summary>
        public float DamageMultiplier { get; set; } = 1f;
        /// <summary>Run speed multiplier while the fists are up (set by PlayerUpgrades).</summary>
        public float SpeedBonus { get; set; } = 1f;

        public bool IsBusy => move != Move.None || pullTarget != null;
        public bool IsFinishing => move == Move.Finisher;
        /// <summary>1-based punch of the combo in progress or just finished; 0 when no combo is going.</summary>
        public int ComboStep => move == Move.Punch || Time.time - lastPunchEnd <= comboWindow ? comboStep + 1 : 0;
        public int ComboLength => comboDamage.Length;
        public IReadOnlyList<float> ComboDamage => comboDamage;
        public float PullCooldownRemaining => Mathf.Max(0f, pullReadyAt - Time.time);
        public float PullCooldown => pullCooldown;
        public float LastHitTime { get; private set; } = float.NegativeInfinity;
        /// <summary>The enemy in the crosshair within lunge (or punch) range, for the HUD's target bar.</summary>
        public Health CurrentTarget { get; private set; }
        /// <summary>Raised when a finisher lands, with its target (tests and effects).</summary>
        public event System.Action<Health> Finished;

        // ---------- Setup ----------

        private void Awake()
        {
            movement = GetComponent<FirstPersonController>();
            health = GetComponent<Health>();
            gear = GetComponent<PlayerGear>();
            var laser = GetComponent<MiningLaser>();
            view = GetComponentInChildren<Camera>(true);
            eye = laser != null && laser.AimOrigin != null ? laser.AimOrigin : view != null ? view.transform : transform;
            InputActionAsset source = laser != null ? laser.InputActions : null;
            if (source == null)
            {
                Debug.LogWarning("PlayerFists needs the MiningLaser's input actions; turning off.", this);
                enabled = false;
                return;
            }
            runtimeActions = Instantiate(source);
            KeyBindings.Register(runtimeActions);
            InputActionMap map = runtimeActions.FindActionMap("Player", true);
            map.bindingMask = InputBinding.MaskByGroups("Keyboard&Mouse", "Gamepad");
            attack = map.FindAction("Attack", true);
            pull = map.FindAction("ChargeShot", true);
        }

        private void Start()
        {
            rage = GetComponent<RageMeter>();
            GetComponent<PlayerUpgrades>()?.Apply(); // this was added after the upgrades first applied
        }

        private void OnEnable()
        {
            runtimeActions?.FindActionMap("Player").Enable();
            waitForRelease = true;
            RenderPipelineManager.beginCameraRendering += ApplyKick;
            RenderPipelineManager.endCameraRendering += RemoveKick;
            ShowKnuckles(true);
            if (movement != null) movement.SetSpeedBonus(this, SpeedBonus);
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= ApplyKick;
            RenderPipelineManager.endCameraRendering -= RemoveKick;
            runtimeActions?.FindActionMap("Player").Disable();
            EndMove();
            CancelPull();
            if (movement != null)
            {
                movement.SetSpeedBonus(this, 1f);
                if (movement.IsLunging) movement.EndLunge();
            }
            kick = Vector3.zero;
            fovKick = 0f;
            lungeTarget = null;
            CurrentTarget = null;
            RageHud.FinisherPrompt = "";
            ClearGlows();
            ShowKnuckles(false);
        }

        private void OnDestroy()
        {
            if (runtimeActions != null) Destroy(runtimeActions);
            foreach (Material material in glowMaterials) if (material != null) Destroy(material);
        }

        // ---------- Every frame ----------

        // LateUpdate so hits use this frame's camera rotation, like the laser.
        private void LateUpdate()
        {
            if (movement != null) movement.SetSpeedBonus(this, SpeedBonus);
            bool attackHeld = attack.IsPressed(), pullHeld = pull.IsPressed();
            bool controllable = Application.isFocused && Cursor.lockState == CursorLockMode.Locked && (movement == null || movement.enabled);
            if (!controllable) { waitForRelease = true; queued = false; }
            else if (waitForRelease && !attackHeld && !pullHeld) waitForRelease = false;
            bool active = controllable && !waitForRelease;

            UpdateTarget();
            UpdateFinishables();
            if (pullTarget != null) UpdatePull();

            if (active && move != Move.Finisher && pullTarget == null)
            {
                if (KnuckleTier >= 3 && KeyBindings.WasPressed("finisher")) TryFinisher();
                else if (KnuckleTier >= 2 && pull.WasPressedThisFrame()) TryPull();
                else if (attack.WasPressedThisFrame())
                {
                    if (move != Move.None) queued = move == Move.Punch;
                    else StartPunch();
                }
            }

            if (lungeTarget != null) UpdateLunge();
            if (move != Move.None) UpdateMove(active);
            UpdateGlow();
            kick = Vector3.Lerp(kick, Vector3.zero, 1f - Mathf.Exp(-14f * Time.deltaTime));
            fovKick = Mathf.Lerp(fovKick, 0f, 1f - Mathf.Exp(-6f * Time.deltaTime));
        }

        private void UpdateTarget()
        {
            float reach = KnuckleTier >= 1 ? lungeRange : range + 0.5f;
            CurrentTarget = MeleeTargets.InSights(eye.position, eye.forward, reach, lungeCone, transform);
        }

        // ---------- Punches ----------

        private void StartPunch()
        {
            bool continues = Time.time - lastPunchEnd <= comboWindow;
            comboStep = continues ? (comboStep + 1) % comboDamage.Length : 0;
            Begin(Move.Punch, comboDuration[Mathf.Min(comboStep, comboDuration.Length - 1)], 0.45f, null);
            // Knuckles I: fling yourself onto the enemy in your sights if it's out of reach.
            if (KnuckleTier >= 1 && CurrentTarget != null && Reach(CurrentTarget) > range * 0.8f) LungeAt(CurrentTarget, lungeSpeed);
        }

        private void Begin(Move kind, float seconds, float strikeAt, Health target)
        {
            move = kind;
            elapsed = 0f;
            length = Mathf.Max(0.05f, seconds);
            hitAt = strikeAt;
            hitApplied = false;
            moveTarget = target;
        }

        private void UpdateMove(bool active)
        {
            if (Time.time >= hitStopUntil) elapsed += Time.deltaTime;
            // Wound up and waiting while a lunge carries you in.
            if (movement != null && movement.IsLunging) elapsed = Mathf.Min(elapsed, hitAt * length * 0.8f);
            float t = elapsed / length;
            if (!hitApplied && t >= hitAt)
            {
                hitApplied = true;
                switch (move)
                {
                    case Move.Punch: Punch(comboStep == comboDamage.Length - 1); break;
                    case Move.Pound: Pound(); break;
                    case Move.Finisher: Finish(); break;
                }
            }
            if (t < 1f) return;
            Move ended = move;
            EndMove();
            if (ended != Move.Punch) return;
            lastPunchEnd = Time.time;
            if (queued && active) StartPunch();
            queued = false;
        }

        private void EndMove()
        {
            if (madeInvulnerable && health != null) health.Invulnerable = invulnerableBefore;
            madeInvulnerable = false;
            move = Move.None;
            moveTarget = null;
            queued = false;
        }

        // One punch: everything in a cone in front, plus the enemy the lunge was aimed at.
        private void Punch(bool heavy)
        {
            float damage = comboDamage[comboStep] * DamageMultiplier;
            int hit = Strike(damage, heavy ? 1.3f : 0.35f, heavy ? 0.7f : 0f, heavy, lungeTarget);
            lungeTarget = null;
            AddKick(heavy ? new Vector3(2.6f, comboStep % 2 == 0 ? 2f : -2f, 2.5f) : new Vector3(1.1f, comboStep % 2 == 0 ? 1.2f : -1.2f, 0f), hit > 0 ? 1f : 0.45f);
            if (hit == 0) Whiff(heavy);
        }

        /// <summary>Hits everything in the punch's cone (and <paramref name="extra"/>); returns how many were hurt.</summary>
        private int Strike(float damage, float shove, float stun, bool heavy, Health extra)
        {
            Vector3 origin = eye.position, forward = eye.forward;
            Vector3 probe = origin + forward * (range * 0.5f);
            struck.Clear();
            struckPoints.Clear();
            int count = Physics.OverlapSphereNonAlloc(origin, range * 1.6f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider candidate = overlaps[i];
                if (candidate.transform.IsChildOf(transform)) continue;
                Health target = MeleeTargets.Owner(candidate);
                if (target == null || target.IsDead || struck.Contains(target)) continue;
                Vector3 point = candidate.bounds.ClosestPoint(probe);
                Vector3 to = point - origin;
                if (new Vector3(to.x, to.y * 0.5f, to.z).magnitude > range) continue; // height counts for less, as in Reach
                // Strict side to side, forgiving up and down: a punch reaches down to critters at your feet.
                Vector3 flatTo = new Vector3(to.x, 0f, to.z), flatForward = new Vector3(forward.x, 0f, forward.z);
                if (flatTo.magnitude > 0.3f && Vector3.Angle(flatForward, flatTo) > arc * 0.5f) continue;
                float pitch = Mathf.Atan2(to.y, Mathf.Max(0.01f, flatTo.magnitude)) * Mathf.Rad2Deg - Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;
                if (pitch < -65f || pitch > 45f) continue;
                struck.Add(target);
                struckPoints.Add(point);
            }
            if (extra != null && !extra.IsDead && !struck.Contains(extra) && Reach(extra) <= range + 0.6f)
            {
                struck.Add(extra);
                struckPoints.Add(MeleeTargets.Centre(extra));
            }

            int hurt = 0;
            for (int i = 0; i < struck.Count; i++)
            {
                Health target = struck[i];
                Vector3 point = struckPoints[i];
                Vector3 back = (origin - point).sqrMagnitude > 0.0001f ? (origin - point).normalized : -forward;
                bool marked = KnuckleTier >= 2 && OverchargeMark.Has(target);
                bool damaged;
                using (RageMeter.As(RageMeter.KillKind.Melee)) damaged = target.TakeDamage(damage, gameObject);
                HitFeedback.Melee(point, back, damaged ? HitFeedback.Result.Damaged : HitFeedback.Result.NoDamage, heavy, GaussFx.Energy);
                if (!damaged) continue;
                hurt++;
                LastHitTime = Time.time;
                glowFlash = 1f;
                if (KnuckleTier >= 1) GaussFx.Pop(point, back, heavy ? 0.9f : 0.5f, heavy ? 14 : 8);
                Vector3 away = target.transform.position - transform.position;
                away.y = 0f;
                if (!target.IsDead && MeleeTargets.IsSmall(target))
                {
                    MeleeTargets.Shove(target, away.normalized * shove);
                    if (stun > 0f) target.GetComponentInParent<IStunnable>()?.Stun(stun);
                }
                if (KnuckleTier < 2) continue;
                // Knuckles II: kills and punches on marked enemies throw chain lightning; other punches leave a mark.
                if (target.IsDead || marked)
                {
                    OverchargeMark.Consume(target);
                    Chain(MeleeTargets.Centre(target), target, 1f);
                }
                else OverchargeMark.Apply(target);
            }
            if (hurt > 0) hitStopUntil = Time.time + hitStop * (heavy ? 1.6f : 1f);
            return hurt;
        }

        private void Chain(Vector3 from, Health skip, float power)
        {
            int jumps = chainJumps + (rage != null ? rage.ExtraJumps : 0);
            float reach = chainReach * (rage != null ? rage.ReachMultiplier : 1f);
            ChainArcs.Fire(from, skip, jumps, reach, chainDamage * DamageMultiplier * power, gameObject);
        }

        // Punched air (or a wall): a knock and dust if something solid was in reach, otherwise just the swish.
        private void Whiff(bool heavy)
        {
            if (Physics.Raycast(eye.position, eye.forward, out RaycastHit hit, range * 0.9f, ~0, QueryTriggerInteraction.Ignore) &&
                !hit.collider.transform.IsChildOf(transform))
                HitFeedback.Melee(hit.point, hit.normal, HitFeedback.Result.Surface, heavy, GaussFx.Energy);
            else Sfx.PlayAt("chain_swish", transform.position, 0.3f, Random.Range(1.4f, 1.6f), 10f);
        }

        // ---------- Knuckles I: the magnet lunge ----------

        // How far the enemy's nearest surface is from the player's eye, along the ground.
        private float Reach(Health target)
        {
            Collider collider = target.GetComponentInChildren<Collider>();
            Vector3 point = collider != null ? collider.bounds.ClosestPoint(eye.position) : target.transform.position;
            Vector3 to = point - eye.position;
            to.y *= 0.5f; // height counts for less: a punch reaches up and down
            return to.magnitude;
        }

        private void LungeAt(Health target, float speed)
        {
            if (movement == null) return;
            Vector3 centre = MeleeTargets.Centre(target);
            Vector3 flat = centre - transform.position;
            flat.y = 0f;
            float distance = flat.magnitude;
            if (distance < 0.5f) return;
            // Stop a punch's length short of its side.
            Collider collider = target.GetComponentInChildren<Collider>();
            float girth = collider != null ? Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z) : 0.4f;
            float stop = girth + 1.1f;
            Vector3 destination = transform.position + flat / distance * Mathf.Max(0f, distance - stop);
            destination.y = Mathf.Clamp(centre.y - 0.6f, transform.position.y - 3f, transform.position.y + 3f);
            Vector3 travel = destination - transform.position;
            float seconds = Mathf.Clamp(travel.magnitude / speed, 0.06f, 0.7f);
            movement.Lunge(travel / seconds, seconds);
            lungeTarget = target;
            fovKick = 9f;
            Sfx.PlayAt("chain_swish", transform.position, 0.6f, Random.Range(0.8f, 0.95f), 15f);
            // A streak of sparks along the way.
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, eye.position + eye.forward * 0.8f, travel, 12, 0.05f, speed * 0.6f, 12f, GaussFx.Energy);
            GaussFx.Bolt(eye.position + eye.forward * 0.6f - eye.up * 0.25f, centre, 0.05f, 0.15f);
        }

        // Arrived (or the target got away): stop the lunge at punching distance.
        private void UpdateLunge()
        {
            if (movement == null || !movement.IsLunging) { if (move == Move.None) lungeTarget = null; return; }
            if (lungeTarget == null || lungeTarget.IsDead || Reach(lungeTarget) <= range * 0.75f) movement.EndLunge();
        }

        // ---------- Knuckles II: the magnet pull and pound ----------

        private void TryPull()
        {
            if (Time.time < pullReadyAt || move != Move.None) return;
            Health target = MeleeTargets.InSights(eye.position, eye.forward, pullRange, pullCone, transform);
            pullReadyAt = Time.time + (target != null ? pullCooldown : 0.4f);
            Vector3 hand = HandPoint(-1f);
            if (target == null)
            {
                // Nothing to grab: the magnet fizzles out in front.
                GaussFx.Bolt(hand, eye.position + eye.forward * 6f, 0.04f, 0.12f);
                Sfx.PlayAt("laser_fizzle", transform.position, 0.5f, 1.3f, 10f);
                return;
            }
            Sfx.PlayAt("chain_swish", transform.position, 0.8f, 0.7f, 20f);
            Sfx.PlayAt("hex_zap", transform.position, 0.6f, 0.8f, 20f);
            AddKick(new Vector3(-1.5f, 0f, 0f), 1f);
            if (MeleeTargets.IsSmall(target))
            {
                // Small: yank it in, then pound it.
                pullTarget = target;
                pullStarted = Time.time;
                target.GetComponentInParent<IStunnable>()?.Stun(1.4f);
                pullBeam = BeamLine();
            }
            else
            {
                // Big: it pulls you to it, and you pound it on arrival.
                LungeAt(target, lungeSpeed * 1.35f);
                GaussFx.Bolt(hand, MeleeTargets.Centre(target), 0.07f, 0.3f, true);
                Begin(Move.Pound, 0.45f, 0.5f, target);
            }
        }

        private void UpdatePull()
        {
            Health target = pullTarget;
            if (target == null || target.IsDead || Time.time - pullStarted > 0.7f)
            {
                bool arrived = target != null && !target.IsDead;
                CancelPull();
                if (arrived) Begin(Move.Pound, 0.4f, 0.45f, target);
                return;
            }
            Vector3 front = transform.position + Flat(eye.forward) * 1.7f;
            Vector3 to = front - target.transform.position;
            to.y = 0f;
            float step = Mathf.Min(to.magnitude, 34f * Time.deltaTime);
            if (to.magnitude > 0.05f && !MeleeTargets.Yank(target, target.transform.position + to.normalized * step))
                to = Vector3.zero; // nowhere to drag it (a wall or a gap): pound it where it is
            if (pullBeam != null) Jag(pullBeam, HandPoint(-1f), MeleeTargets.Centre(target));
            if (to.magnitude < 0.4f)
            {
                CancelPull();
                Begin(Move.Pound, 0.4f, 0.45f, target);
            }
        }

        private void CancelPull()
        {
            pullTarget = null;
            if (pullBeam != null) Destroy(pullBeam.gameObject);
            pullBeam = null;
        }

        // Both fists down on whatever is in front: heavy damage, a jolt and a mark (or lightning if it was marked).
        private void Pound()
        {
            Health target = moveTarget;
            if (movement != null && movement.IsLunging) movement.EndLunge();
            int hit = Strike(poundDamage * DamageMultiplier, 1.6f, 0.9f, true, target);
            GaussFx.Ring(transform.position, 3f, 0.3f, 0.2f);
            CameraShake.Add(hit > 0 ? 0.22f : 0.1f);
            AddKick(new Vector3(4f, 0f, 0f), 1f);
            if (hit == 0) Whiff(true);
        }

        // ---------- Knuckles III: finishers ----------

        public bool CanFinish(Health target) =>
            target != null && !target.IsDead && !target.Invulnerable && MeleeTargets.IsSmall(target) && target.Current <= target.Max * finishBelow;

        /// <summary>The finishable enemy V would go for right now (in the sights, or the nearest close by), or null.</summary>
        public Health FinisherTarget()
        {
            Health target = MeleeTargets.InSights(eye.position, eye.forward, finishRange, 25f, transform, CanFinish);
            if (target != null) return target;
            foreach (Health candidate in MeleeTargets.Around(transform.position, 3f, transform, nearby))
                if (CanFinish(candidate)) return candidate;
            return null;
        }

        private void TryFinisher()
        {
            Health target = FinisherTarget();
            if (target == null || move != Move.None) return;
            if (health != null)
            {
                invulnerableBefore = health.Invulnerable;
                health.Invulnerable = true;
                madeInvulnerable = true;
            }
            target.GetComponentInParent<IStunnable>()?.Stun(2f);
            if (Reach(target) > range * 0.7f) LungeAt(target, lungeSpeed * 1.2f);
            Begin(Move.Finisher, 0.72f, 0.55f, target); // invulnerable until it ends (EndMove)
            Sfx.PlayAt("laser_charge_full", transform.position, 0.8f, 0.85f, 20f);
            ShowGlow(target, false);
        }

        private void Finish()
        {
            Health target = moveTarget;
            if (movement != null && movement.IsLunging) movement.EndLunge();
            Vector3 at = target != null ? MeleeTargets.Centre(target) : transform.position + Flat(eye.forward) * 1.5f;
            if (target != null && !target.IsDead)
                using (RageMeter.As(RageMeter.KillKind.Finisher)) target.TakeDamage(target.Current + 1f, gameObject);
            LastHitTime = Time.time;
            glowFlash = 1.6f;

            float reach = rage != null ? rage.ReachMultiplier : 1f;
            float radius = shockRadius * reach;
            Vector3 ground = target != null ? target.transform.position : transform.position;
            // The shockwave: everything around is hurt, shoved away and (if small) knocked silly.
            foreach (Health other in MeleeTargets.Around(at, radius, transform, nearby).ToArray())
            {
                if (other == target) continue;
                float share = Mathf.Lerp(1f, 0.5f, Vector3.Distance(at, other.transform.position) / radius);
                other.TakeDamage(shockDamage * DamageMultiplier * share, gameObject);
                if (other.IsDead || !MeleeTargets.IsSmall(other)) continue;
                MeleeTargets.Shove(other, Flat(other.transform.position - at) * 2.5f * share);
                other.GetComponentInParent<IStunnable>()?.Stun(1f);
            }
            Chain(at, target, 1.5f);
            // Every overcharge mark nearby goes off.
            foreach (OverchargeMark mark in new List<OverchargeMark>(OverchargeMark.All))
                if (mark != null && mark.Health != target && Vector3.Distance(mark.transform.position, ground) <= radius * 1.6f)
                    mark.Detonate(markBurstDamage * DamageMultiplier, markBurstReach * reach, gameObject);

            GaussFx.Pop(at, Vector3.up, 2.2f, 40);
            GaussFx.Ring(ground, radius, 0.45f, 0.45f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, ground, Vector3.up, 24, 0.5f, 5f, 70f, new Color(0.55f, 0.52f, 0.48f, 0.6f));
            Sfx.PlayAt("laser_blast", at, 1f, 0.7f, 40f);
            Sfx.PlayAt("bolt_impact", at, 1f, 0.55f, 40f);
            CameraShake.Add(0.5f);
            AddKick(new Vector3(7f, 0f, 0f), 1f);
            fovKick = -6f;
            Finished?.Invoke(target);
        }

        // ---------- Finisher glows ----------

        private void UpdateFinishables()
        {
            if (KnuckleTier < 3)
            {
                RageHud.FinisherPrompt = "";
                if (glows.Count > 0) ClearGlows();
                return;
            }
            Health next = move == Move.Finisher ? null : FinisherTarget();
            RageHud.FinisherPrompt = next != null ? RageHud.FinishText(next.name.Replace("(Clone)", "").Trim()) : "";
            if (Time.time < nextGlowScan) return;
            nextGlowScan = Time.time + 0.2f;
            var keep = new HashSet<Health>();
            foreach (Health candidate in MeleeTargets.Around(transform.position, 22f, transform, nearby))
                if (CanFinish(candidate) && candidate != moveTarget) keep.Add(candidate);
            foreach (Health candidate in keep) ShowGlow(candidate, true);
            foreach (Health shown in new List<Health>(glows.Keys))
                if (shown == null || !keep.Contains(shown)) ShowGlow(shown, false);
        }

        private void ShowGlow(Health target, bool show)
        {
            glows.TryGetValue(target, out FinisherGlow glow);
            if (show && glow == null && target != null) glows[target] = target.gameObject.AddComponent<FinisherGlow>();
            else if (!show)
            {
                if (glow != null) Destroy(glow);
                glows.Remove(target);
            }
        }

        private void ClearGlows()
        {
            foreach (FinisherGlow glow in glows.Values) if (glow != null) Destroy(glow);
            glows.Clear();
        }

        // ---------- The knuckles on the hands ----------

        private void ShowKnuckles(bool show)
        {
            show &= KnuckleTier >= 1;
            if (show && rightKnuckles.parts == null) FitKnuckles();
            foreach (var fit in new[] { rightKnuckles, leftKnuckles })
                if (fit.parts != null)
                    foreach (GameObject part in fit.parts) if (part != null) part.SetActive(show);
        }

        private void FitKnuckles()
        {
            var hands = GetComponent<FirstPersonHands>();
            if (hands == null || hands.RightHand == null || hands.LeftHand == null) return;
            rightKnuckles = MeleeGearModels.FitKnuckles(hands.RightHand, 1f);
            leftKnuckles = MeleeGearModels.FitKnuckles(hands.LeftHand, -1f);
            // Their own copies of the glow material, so the punches can pulse it.
            foreach (Renderer renderer in rightKnuckles.glowing.Concat(leftKnuckles.glowing))
            {
                Material[] materials = renderer.materials;
                foreach (Material material in materials)
                    if (material.IsKeywordEnabled("_EMISSION")) glowMaterials.Add(material);
            }
        }

        private void UpdateGlow()
        {
            if (KnuckleTier >= 1 && rightKnuckles.parts == null) ShowKnuckles(true); // bought with the fists up
            if (glowMaterials.Count == 0) return;
            glowFlash = Mathf.MoveTowards(glowFlash, 0f, Time.deltaTime * 3f);
            float stacks = rage != null ? rage.Stacks : 0f;
            float pulse = rage != null && rage.Maxed ? 0.6f + 0.4f * Mathf.Sin(Time.time * 10f) : 0f;
            float charge = (pullTarget != null || move == Move.Finisher) ? 1.5f : 0f;
            float intensity = 1.4f + stacks * 0.18f + glowFlash * 3f + pulse + charge;
            Color color = Color.Lerp(GaussFx.Energy, Color.white, Mathf.Clamp01(glowFlash * 0.4f + pulse * 0.3f));
            foreach (Material material in glowMaterials)
                if (material != null) material.SetColor("_EmissionColor", color * intensity);
        }

        // ---------- Hand poses ----------

        // Camera-space poses for the right hand (side 1); the left mirrors them. Rotations are (pitch, yaw, roll).
        // The guard: fists up, turned half over so the backs of the hands (the gauss coils) face up toward the eyes.
        private static readonly Vector3 GuardRight = new Vector3(0.17f, -0.19f, 0.4f), GuardRightAngles = new Vector3(-28f, -14f, -38f);
        private static readonly Vector3 GuardLeft = new Vector3(0.19f, -0.16f, 0.46f), GuardLeftAngles = new Vector3(-30f, -10f, -34f);

        private readonly struct Pose
        {
            public readonly Vector3 Position, Angles;
            public Pose(Vector3 position, Vector3 angles) { Position = position; Angles = angles; }
            public static Pose Lerp(Pose a, Pose b, float t) => new Pose(Vector3.LerpUnclamped(a.Position, b.Position, t), Vector3.LerpUnclamped(a.Angles, b.Angles, t));
        }

        /// <summary>
        /// Where a hand goes right now (<paramref name="side"/> 1 right, -1 left), in camera space. Returns false when the
        /// hand is free to swing with the run (no punch going).
        /// </summary>
        public bool GetPose(float side, out Vector3 position, out Quaternion rotation)
        {
            Pose guard = Guard(side);
            Pose pose = guard;
            bool busy = move != Move.None || pullTarget != null;
            if (move != Move.None)
            {
                (Pose windup, Pose strike, bool moving) = Strike(side);
                if (moving) pose = Timeline(guard, windup, strike, elapsed / length);
            }
            else if (pullTarget != null && side < 0f)
                pose = new Pose(new Vector3(0.14f, -0.1f, 0.58f), new Vector3(-10f, 4f, -20f)); // the magnet hand thrust out
            // A gentle breathing bob, the hands out of step.
            float bob = Mathf.Sin(Time.time * 2.2f + (side > 0f ? 0f : 1.4f)) * 0.006f;
            position = new Vector3(pose.Position.x * side, pose.Position.y + bob, pose.Position.z);
            rotation = Quaternion.Euler(pose.Angles.x, pose.Angles.y * side, pose.Angles.z * side);
            return busy;
        }

        private static Pose Guard(float side) => side > 0f ? new Pose(GuardRight, GuardRightAngles) : new Pose(GuardLeft, GuardLeftAngles);

        // The windup and strike poses of the current move for this hand, and whether this hand moves at all.
        private (Pose windup, Pose strike, bool moving) Strike(float side)
        {
            bool right = side > 0f;
            switch (move)
            {
                case Move.Punch:
                    if (comboStep == 0) // jab: the lead (left) hand straight out
                        return (new Pose(GuardLeft + new Vector3(0f, 0f, -0.05f), GuardLeftAngles), new Pose(new Vector3(0.07f, -0.1f, 0.72f), new Vector3(-4f, -6f, -12f)), !right);
                    if (comboStep == 1) // cross: the right hand, turning over
                        return (new Pose(GuardRight + new Vector3(0.02f, -0.02f, -0.07f), GuardRightAngles), new Pose(new Vector3(0.05f, -0.11f, 0.74f), new Vector3(-4f, -8f, -15f)), right);
                    // hook: the left hand swings round from the side
                    return (new Pose(new Vector3(0.38f, -0.13f, 0.3f), new Vector3(-10f, -15f, -20f)), new Pose(new Vector3(-0.04f, -0.06f, 0.6f), new Vector3(-6f, -55f, -5f)), !right);
                case Move.Pound:
                    return (new Pose(new Vector3(0.15f, 0.0f, 0.44f), new Vector3(-35f, -10f, -20f)), new Pose(new Vector3(0.1f, -0.3f, 0.56f), new Vector3(16f, -6f, -10f)), true);
                case Move.Finisher:
                    return (new Pose(new Vector3(0.17f, 0.05f, 0.46f), new Vector3(-42f, -12f, -25f)), new Pose(new Vector3(0.09f, -0.33f, 0.58f), new Vector3(20f, -4f, -8f)), true);
            }
            return (default, default, false);
        }

        // Guard to windup, a fast snap to the strike pose exactly when the hit lands, a short hold, then back to guard.
        private Pose Timeline(Pose guard, Pose windup, Pose strike, float t)
        {
            float wound = hitAt * 0.55f, hold = Mathf.Min(1f, hitAt + 0.14f);
            if (t < wound) return Pose.Lerp(guard, windup, Mathf.SmoothStep(0f, 1f, t / wound));
            if (t < hitAt)
            {
                float k = (t - wound) / (hitAt - wound);
                return Pose.Lerp(windup, strike, k * k);
            }
            if (t < hold) return strike;
            return Pose.Lerp(strike, guard, Mathf.SmoothStep(0f, 1f, (t - hold) / Mathf.Max(0.01f, 1f - hold)));
        }

        // Where a hand is now, in the world (the magnet beam starts at the left fist).
        private Vector3 HandPoint(float side)
        {
            var hands = GetComponent<FirstPersonHands>();
            Transform hand = hands == null ? null : side > 0f ? hands.RightHand : hands.LeftHand;
            return hand != null ? hand.position + eye.forward * 0.06f : eye.position + eye.forward * 0.5f + eye.right * 0.2f * side - eye.up * 0.2f;
        }

        // ---------- Feel ----------

        private void AddKick(Vector3 degrees, float scale) => kick += degrees * scale;

        private void ApplyKick(ScriptableRenderContext context, Camera rendering)
        {
            if (rendering != view || view == null || (kick.sqrMagnitude < 0.0001f && Mathf.Abs(fovKick) < 0.05f)) return;
            unkicked = eye.localRotation;
            unkickedFov = view.fieldOfView;
            eye.localRotation = unkicked * Quaternion.Euler(kick);
            view.fieldOfView = unkickedFov + fovKick;
            kickApplied = true;
        }

        private void RemoveKick(ScriptableRenderContext context, Camera rendering)
        {
            if (!kickApplied || rendering != view) return;
            eye.localRotation = unkicked;
            view.fieldOfView = unkickedFov;
            kickApplied = false;
        }

        private LineRenderer BeamLine()
        {
            var line = new GameObject("Magnet Beam").AddComponent<LineRenderer>();
            line.sharedMaterial = GaussFx.EnergyMaterial;
            line.useWorldSpace = true;
            line.positionCount = 10;
            line.widthMultiplier = 0.05f;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static void Jag(LineRenderer line, Vector3 a, Vector3 b)
        {
            for (int i = 0; i < line.positionCount; i++)
            {
                float t = i / (float)(line.positionCount - 1);
                Vector3 wobble = Random.insideUnitSphere * 0.12f * Mathf.Sin(t * Mathf.PI);
                line.SetPosition(i, Vector3.Lerp(a, b, t) + wobble);
            }
            line.widthMultiplier = 0.04f + 0.03f * Random.value;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }
    }
}
