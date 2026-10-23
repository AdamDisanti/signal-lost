using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace SignalLost.Combat
{
    /// <summary>
    /// The player's mining laser, used as a melee tool with a charged shot.
    /// Attack (left click / gamepad west): a three-hit combo of a jab, a side bash and an overhead slam;
    /// the slam hits hardest. Each strike winds up, lands, pauses briefly on contact and recovers.
    /// Charge Shot (hold right click / right trigger): charges while held. Releasing fires a bolt whose
    /// damage, speed and size scale with how long it was held. A short cooldown follows each shot.
    /// The held model comes from the Held Model prefab (Lawson's mining laser) when one is assigned,
    /// otherwise a placeholder box is built. Charging crackles with electricity at the muzzle (ChargeEffect).
    /// All numbers are placeholders to tune in the Inspector during playtesting.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiningLaser : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputActionAsset inputActions;
        [Tooltip("Where aim comes from; normally the player camera.")]
        [SerializeField] private Transform aimOrigin;
        [Tooltip("Where the tool is held; a point slightly below and right of the camera.")]
        [SerializeField] private Transform muzzle;
        [Tooltip("Its material colours the melee impact flashes. The line itself is not drawn.")]
        [SerializeField] private LineRenderer beam;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Held model")]
        [Tooltip("Prefab shown in the player's hand. Child objects named GripAnchor and MuzzleAnchor set where it " +
                 "is held and where shots leave it. Its colliders are removed. Leave empty for a placeholder box.")]
        [SerializeField] private GameObject heldModel;
        [SerializeField, Min(0.01f)] private float heldModelScale = 0.55f;
        [Tooltip("Where the grip sits, relative to the Muzzle transform.")]
        [SerializeField] private Vector3 gripPosition = new Vector3(-0.02f, -0.08f, -0.22f);
        [Tooltip("Rotation of the held model in degrees. A small negative Y angles it toward the crosshair.")]
        [SerializeField] private Vector3 heldModelRotation = new Vector3(0f, -3f, 0f);

        [Header("Melee combo (placeholder values)")]
        [Tooltip("Damage of each swing in order. The number of entries is the combo length.")]
        [SerializeField] private float[] comboDamage = { 10f, 10f, 20f };
        [Tooltip("Seconds each swing takes, including recovery. Same order as Combo Damage.")]
        [SerializeField] private float[] comboDuration = { 0.4f, 0.4f, 0.65f };
        [SerializeField, Min(0.5f)] private float meleeRange = 2.1f;
        [Tooltip("Width of each strike in degrees, centred on the crosshair, in the same order as Combo Damage. " +
                 "Everything inside it is hit. The jab is narrow; the side bash sweeps wider.")]
        [SerializeField] private float[] comboArc = { 30f, 70f, 45f };
        [Tooltip("Seconds after a swing ends during which another press continues the combo.")]
        [SerializeField, Min(0f)] private float comboWindow = 0.4f;
        [Tooltip("Seconds a strike freezes when it connects, which makes hits feel heavier.")]
        [SerializeField, Min(0f)] private float hitStop = 0.06f;
        [Tooltip("Seconds after the last swing of a full combo before you can swing again. 0 turns it off.")]
        [SerializeField, Min(0f)] private float comboCooldown;
        [Tooltip("Seconds the last swing of the combo stuns enemies that can be stunned (IStunnable; not bosses). " +
                 "Nothing else stuns them.")]
        [SerializeField, Min(0f)] private float finisherStun = 1f;

        [Header("Charge effect")]
        [Tooltip("Colour of the charging electricity, the bolt, and the flash where a bolt lands.")]
        [SerializeField] private Color energyColor = new Color(1f, 0.549f, 0f); // dark orange

        [Header("Feel")]
        [Tooltip("Strength of the camera kick on strikes and shots. 0 turns it off.")]
        [SerializeField, Min(0f)] private float viewKick = 0.9f;
        [Tooltip("How much the laser lags behind when you look around. 0 turns it off.")]
        [SerializeField, Min(0f)] private float swayStrength = 1f;
        [Tooltip("How much the laser bobs while walking and sprinting, dips on landing, and drifts while idle. 0 turns it off.")]
        [SerializeField, Min(0f)] private float bobStrength = 1f;

        [Header("Charged shot (placeholder values)")]
        [Tooltip("Seconds of holding to reach full charge. Releasing earlier fires a weaker shot.")]
        [SerializeField, Min(0.1f)] private float fullChargeTime = 1.2f;
        [SerializeField, Min(0f)] private float minShotDamage = 5f;
        [SerializeField, Min(0f)] private float maxShotDamage = 50f;
        [SerializeField, Min(1f)] private float minShotSpeed = 25f;
        [SerializeField, Min(1f)] private float maxShotSpeed = 45f;
        [SerializeField, Min(0.01f)] private float minShotRadius = 0.06f;
        [SerializeField, Min(0.01f)] private float maxShotRadius = 0.2f;
        [Tooltip("Distance a bolt travels before fading out.")]
        [SerializeField, Min(1f)] private float shotRange = 30f;
        [Tooltip("Seconds after a full-charge shot before charging can start again.")]
        [SerializeField, Min(0f)] private float shotCooldown = 0.5f;
        [Tooltip("Seconds after a shot charged 10% or less. Cooldowns scale linearly between this and Shot Cooldown " +
                 "(10% = this, 100% = Shot Cooldown). Negative: always use Shot Cooldown.")]
        [SerializeField] private float tapShotCooldown = -1f;

        [Header("Full-charge blast")]
        [Tooltip("A full-charge bolt bursts on impact, hurting everything this close. 0 turns it off.")]
        [SerializeField, Min(0f)] private float fullChargeBlastRadius = 4f;
        [Tooltip("Share of the bolt's damage dealt at the edge of the blast (full damage at the centre).")]
        [SerializeField, Range(0f, 1f)] private float blastEdgeDamage = 0.5f;
        [Tooltip("How far the blast shoves enemies at its centre.")]
        [SerializeField, Min(0f)] private float blastKnockback = 1.2f;

        /// <summary>
        /// One melee strike, as poses relative to the resting hold (positions in metres, rotations in degrees:
        /// x tips the muzzle down, y turns it left/right, z rolls it). The strike pose is reached exactly
        /// when the hit lands, at HitAt of the way through the swing.
        /// </summary>
        private readonly struct Strike
        {
            public readonly Vector3 WindupPosition, WindupRotation, StrikePosition, StrikeRotation, Kick;
            public readonly float HitAt;

            public Strike(Vector3 windupPosition, Vector3 windupRotation, Vector3 strikePosition,
                Vector3 strikeRotation, float hitAt, Vector3 kick)
            {
                WindupPosition = windupPosition;
                WindupRotation = windupRotation;
                StrikePosition = strikePosition;
                StrikeRotation = strikeRotation;
                HitAt = hitAt;
                Kick = kick;
            }
        }

        // Jab: pull back, then thrust the muzzle straight at the crosshair.
        private static readonly Strike Jab = new Strike(
            new Vector3(0.02f, -0.02f, -0.1f), new Vector3(8f, 6f, 0f),
            new Vector3(-0.1f, 0.04f, 0.22f), new Vector3(-4f, -12f, 0f),
            0.3f, new Vector3(1.2f, 0f, 0f));

        // Side bash: cock the laser out to the right, then hook it across, hitting with its body.
        private static readonly Strike Bash = new Strike(
            new Vector3(0.1f, 0.03f, -0.04f), new Vector3(0f, 30f, 25f),
            new Vector3(-0.22f, 0.02f, 0.1f), new Vector3(5f, -55f, -35f),
            0.35f, new Vector3(0.5f, -1.5f, 1.5f));

        // Overhead slam: raise it muzzle-up, then bring it crashing down.
        private static readonly Strike Slam = new Strike(
            new Vector3(0f, 0.16f, -0.08f), new Vector3(-55f, 0f, -10f),
            new Vector3(-0.08f, -0.06f, 0.14f), new Vector3(35f, -8f, 5f),
            0.45f, new Vector3(3f, 0f, 0f));

        private const float StrikeHold = 0.12f; // part of a swing spent holding the strike pose before recovering

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private readonly Collider[] overlaps = new Collider[32];
        private readonly List<Health> struckThisSwing = new List<Health>();

        private InputActionAsset runtimeActions;
        private InputActionMap playerMap;
        private InputAction attack, chargeShot;
        private bool waitForRelease;

        private int comboStep;
        private float swingElapsed, swingLength, hitStopUntil;
        private float lastSwingEnd = float.NegativeInfinity;
        private bool hitApplied, queuedSwing;

        private float chargeStart, cooldownUntil, cooldownLength, comboCooldownUntil, recoil;

        // Held-laser motion: smoothed base pose, plus sway, bob and landing offsets added on top.
        private Vector3 basePosition;
        private Quaternion baseRotation = Quaternion.identity;
        private CharacterController body;
        private Quaternion lastAim;
        private Vector2 sway;            // x: pitch lag, y: yaw lag, in degrees
        private float bobPhase, bobWeight, airLift, landDip, landDipTarget, lastVerticalSpeed, airTime;
        private bool wasGrounded = true;

        private Camera viewCamera;
        private Vector3 kick;            // current camera kick in degrees, decays to zero
        private Quaternion unkickedRotation;
        private bool kickApplied;

        private Transform toolPivot, muzzlePoint;
        private Vector3 restPosition;
        private ChargeEffect chargeEffect;
        private Material toolMaterial, energyMaterial, energyFullMaterial, energyCoreMaterial;
        private bool fullChargeReached;
        private float fullChargePop; // 1 the moment full charge is reached, decays to 0

        /// <summary>0 to 1 while the shot is charging; 0 otherwise.</summary>
        public float Charge01 => IsCharging ? Mathf.Clamp01((Time.time - chargeStart) / fullChargeTime) : 0f;
        public bool IsCharging { get; private set; }
        public bool IsSwinging { get; private set; }
        /// <summary>1-based number of the swing in progress or just finished; 0 when no combo is active.</summary>
        public int ComboStep => IsSwinging || Time.time - lastSwingEnd <= comboWindow ? comboStep + 1 : 0;
        public int ComboLength => Mathf.Max(1, Mathf.Min(comboDamage.Length, comboDuration.Length));
        /// <summary>True between firing a shot and being able to charge again.</summary>
        public bool IsCoolingDown => Time.time < cooldownUntil;
        /// <summary>Seconds until the charged shot can be used again; 0 when ready.</summary>
        public float ShotCooldownRemaining => Mathf.Max(0f, cooldownUntil - Time.time);
        /// <summary>Seconds until melee swings are possible again after a full combo; 0 when ready.</summary>
        public float ComboCooldownRemaining => Mathf.Max(0f, comboCooldownUntil - Time.time);
        /// <summary>Cooldown after a full-charge shot (the longest).</summary>
        public float ShotCooldown => shotCooldown;
        /// <summary>Cooldown after a weak tap shot (the shortest).</summary>
        public float TapShotCooldown => tapShotCooldown < 0f ? shotCooldown : tapShotCooldown;
        /// <summary>Length of the cooldown currently running (it depends on how charged the last shot was).</summary>
        public float CurrentCooldownLength => cooldownLength;
        public float FullChargeBlastRadius => fullChargeBlastRadius;
        /// <summary>The held laser's grip point; it moves with every swing (hands attach here).</summary>
        public Transform HeldTool => toolPivot;

        /// <summary>Cooldown for a shot at this charge: 10% or less uses the tap cooldown, scaling up to full charge.</summary>
        public float CooldownFor(float charge) => Mathf.Lerp(TapShotCooldown, shotCooldown, Mathf.InverseLerp(0.1f, 1f, charge));
        public float ComboCooldown => comboCooldown;
        public float FinisherStun => finisherStun;
        public float MinShotDamage => minShotDamage;
        public float MaxShotDamage => maxShotDamage;
        public float ShotRange => shotRange;
        /// <summary>Damage of each swing of the combo, in order.</summary>
        public IReadOnlyList<float> ComboDamage => comboDamage;
        /// <summary>Time.time of the last melee or bolt hit on something with Health, for hit markers.</summary>
        public float LastHitTime { get; private set; } = float.NegativeInfinity;
        /// <summary>The living damageable object under the crosshair within shot range, or null.</summary>
        public Health CurrentTarget { get; private set; }

        private void Awake()
        {
            if (inputActions == null || aimOrigin == null || muzzle == null || beam == null)
            {
                Debug.LogError("MiningLaser requires input actions, an aim origin, a muzzle, and a LineRenderer.", this);
                enabled = false;
                return;
            }
            if (comboDamage.Length == 0 || comboDuration.Length == 0)
            {
                Debug.LogError("MiningLaser needs at least one entry in Combo Damage and Combo Duration.", this);
                enabled = false;
                return;
            }

            // Same pattern as FirstPersonController: use a private copy so components never fight over the shared asset.
            runtimeActions = Instantiate(inputActions);
            playerMap = runtimeActions.FindActionMap("Player", true);
            playerMap.bindingMask = InputBinding.MaskByGroups("Keyboard&Mouse", "Gamepad");
            attack = playerMap.FindAction("Attack", true);
            chargeShot = playerMap.FindAction("ChargeShot", true);

            beam.enabled = false;
            viewCamera = aimOrigin.GetComponent<Camera>();
            body = GetComponent<CharacterController>();
            lastAim = aimOrigin.rotation;

            if (heldModel != null) BuildHeldModel();
            else BuildPlaceholderTool();
            basePosition = restPosition;
            toolPivot.gameObject.SetActive(isActiveAndEnabled);
        }

        private void OnEnable()
        {
            if (toolPivot != null) toolPivot.gameObject.SetActive(true);
            waitForRelease = true;
            playerMap?.Enable();
            RenderPipelineManager.beginCameraRendering += ApplyKick;
            RenderPipelineManager.endCameraRendering += RemoveKick;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= ApplyKick;
            RenderPipelineManager.endCameraRendering -= RemoveKick;
            kick = Vector3.zero;
            if (toolPivot != null) toolPivot.gameObject.SetActive(false);
            playerMap?.Disable();
            IsCharging = false;
            IsSwinging = false;
            queuedSwing = false;
            CurrentTarget = null;
            if (beam != null) beam.enabled = false;
            if (chargeEffect != null) chargeEffect.Show(false, 0f);
        }

        private void OnDestroy()
        {
            if (runtimeActions != null) Destroy(runtimeActions);
            if (toolMaterial != null) Destroy(toolMaterial);
            if (energyMaterial != null) Destroy(energyMaterial);
            if (energyCoreMaterial != null) Destroy(energyCoreMaterial);
            if (energyFullMaterial != null) Destroy(energyFullMaterial);
        }

        // LateUpdate so hits and visuals use this frame's camera rotation from FirstPersonController.Update.
        private void LateUpdate()
        {
            bool attackHeld = attack.IsPressed();
            bool chargeHeld = chargeShot.IsPressed();
            bool controllable = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;

            // The left click that re-captures the cursor must not also swing. While the cursor is free,
            // cancel any charge and require both buttons to be released before acting again.
            if (!controllable)
            {
                waitForRelease = true;
                IsCharging = false;
                queuedSwing = false;
            }
            else if (waitForRelease && !attackHeld && !chargeHeld)
            {
                waitForRelease = false;
            }
            bool active = controllable && !waitForRelease;

            // Charged shot: releasing fires; holding starts a charge once no swing or cooldown is in the way.
            if (IsCharging && !chargeHeld)
            {
                FireBolt(Charge01);
                IsCharging = false;
            }
            else if (!IsCharging && active && chargeHeld && !IsSwinging && !IsCoolingDown)
            {
                IsCharging = true;
                chargeStart = Time.time;
                queuedSwing = false;
                fullChargeReached = false;
            }

            // The moment full charge is reached: a small kick and a jolt of the laser (ChargeEffect adds the burst).
            if (IsCharging && !fullChargeReached && Charge01 >= 1f)
            {
                fullChargeReached = true;
                fullChargePop = 1f;
                AddKick(new Vector3(-0.8f, 0f, 0.4f));
            }

            // Melee: a press during a swing is remembered and chains into the next swing.
            if (active && !IsCharging && attack.WasPressedThisFrame() && ComboCooldownRemaining <= 0f)
            {
                if (IsSwinging) queuedSwing = !(IsFinisher && comboCooldown > 0f);
                else StartSwing();
            }

            if (IsSwinging) UpdateSwing(active);

            UpdateTarget();
            UpdateVisuals();
        }

        private void StartSwing()
        {
            bool continuesCombo = Time.time - lastSwingEnd <= comboWindow;
            comboStep = continuesCombo ? (comboStep + 1) % ComboLength : 0;
            swingElapsed = 0f;
            swingLength = Mathf.Max(0.05f, comboDuration[comboStep]);
            hitApplied = false;
            IsSwinging = true;
        }

        private void UpdateSwing(bool active)
        {
            // The swing clock stops during hit-stop, freezing the laser at the moment of impact.
            if (Time.time >= hitStopUntil) swingElapsed += Time.deltaTime;
            float t = swingElapsed / swingLength;
            Strike strike = CurrentStrike();
            if (!hitApplied && t >= strike.HitAt)
            {
                hitApplied = true;
                bool connected = MeleeHit(comboDamage[comboStep], IsFinisher ? finisherStun : 0f);
                if (connected) hitStopUntil = Time.time + hitStop;
                AddKick(strike.Kick * (connected ? 1f : 0.5f));
            }
            if (t < 1f) return;

            IsSwinging = false;
            lastSwingEnd = Time.time;
            if (IsFinisher && comboCooldown > 0f)
            {
                // The finisher ends the combo; the laser needs a moment before the next one.
                comboCooldownUntil = Time.time + comboCooldown;
                lastSwingEnd = float.NegativeInfinity;
                queuedSwing = false;
            }
            if (queuedSwing && active)
            {
                queuedSwing = false;
                StartSwing();
            }
            queuedSwing = false;
        }

        private bool IsFinisher => ComboLength > 1 && comboStep == ComboLength - 1;

        /// <summary>Damages everything inside the swing and stuns what can be stunned. Returns true if anything was hit.</summary>
        private bool MeleeHit(float damage, float stunSeconds)
        {
            Vector3 origin = aimOrigin.position;
            Vector3 forward = aimOrigin.forward;
            Vector3 probe = origin + forward * (meleeRange * 0.5f);
            float halfArc = StrikeArc(comboStep) * 0.5f;
            struckThisSwing.Clear();

            int count = Physics.OverlapSphereNonAlloc(origin, meleeRange, overlaps, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider candidate = overlaps[i];
                if (candidate.transform.IsChildOf(transform)) continue;
                Health target = candidate.GetComponentInParent<Health>();
                if (target == null || target.IsDead || struckThisSwing.Contains(target)) continue;

                // Measure to the part of the target nearest the middle of the swing.
                Vector3 point = candidate.bounds.ClosestPoint(probe);
                Vector3 toPoint = point - origin;
                float distance = toPoint.magnitude;
                if (distance > meleeRange) continue;
                if (distance > 0.1f && Vector3.Angle(forward, toPoint) > halfArc) continue;
                if (distance > 0.1f && IsBlocked(origin, toPoint / distance, distance, target)) continue;

                struckThisSwing.Add(target);
                ImpactFlash.Spawn(beam.sharedMaterial, point, 0.3f);
            }

            bool connected = false;
            foreach (Health target in struckThisSwing)
            {
                if (!target.TakeDamage(damage, gameObject)) continue;
                LastHitTime = Time.time;
                connected = true;
                if (stunSeconds > 0f && !target.IsDead) target.GetComponentInParent<IStunnable>()?.Stun(stunSeconds);
            }
            return connected;
        }

        /// <summary>Width of a strike in degrees; uses the last entry if the list is shorter than the combo.</summary>
        private float StrikeArc(int step)
        {
            if (comboArc == null || comboArc.Length == 0) return 45f;
            return Mathf.Clamp(comboArc[Mathf.Min(step, comboArc.Length - 1)], 5f, 180f);
        }

        /// <summary>True if something other than the player or the target lies between origin and the target.</summary>
        private bool IsBlocked(Vector3 origin, Vector3 direction, float distance, Health target)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider other = hits[i].collider;
                if (other.transform.IsChildOf(transform)) continue;
                if (other.GetComponentInParent<Health>() == target) continue;
                if (hits[i].distance < distance - 0.05f) return true;
            }
            return false;
        }

        private void FireBolt(float charge)
        {
            // Aim where the crosshair points, but launch from the tool tip.
            Vector3 origin = aimOrigin.position;
            Vector3 aimPoint = origin + aimOrigin.forward * shotRange;
            if (TryNearestHit(origin, aimOrigin.forward, shotRange, out RaycastHit hit)) aimPoint = hit.point;

            Vector3 tip = muzzlePoint.position;
            Vector3 direction = aimPoint - tip;
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : aimOrigin.forward;

            LaserBolt bolt = LaserBolt.Spawn(
                energyMaterial, tip, direction, origin,
                Mathf.Lerp(minShotDamage, maxShotDamage, charge),
                Mathf.Lerp(minShotSpeed, maxShotSpeed, charge),
                Mathf.Lerp(minShotRadius, maxShotRadius, charge),
                shotRange, hitMask, transform, OnBoltHit);
            if (charge >= 1f && fullChargeBlastRadius > 0f) bolt.SetBlast(fullChargeBlastRadius, blastEdgeDamage, blastKnockback);

            cooldownLength = CooldownFor(charge);
            cooldownUntil = Time.time + cooldownLength;
            recoil = 1f;
            AddKick(new Vector3(-1f - 1.5f * charge, 0f, 0f));
        }

        private void OnBoltHit(Health target) => LastHitTime = Time.time;

        private void UpdateTarget()
        {
            CurrentTarget = null;
            if (!TryNearestHit(aimOrigin.position, aimOrigin.forward, shotRange, out RaycastHit hit)) return;
            Health target = hit.collider.GetComponentInParent<Health>();
            if (target == null || target.IsDead) return;
            var merchant = target.GetComponent<SignalLost.NPC.MerchantCrab>();
            if (merchant != null && merchant.IsDefeated) return;
            CurrentTarget = target;
        }

        /// <summary>Nearest raycast hit that is not part of the player.</summary>
        private bool TryNearestHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            bool found = false;
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(transform)) continue;
                if (found && hits[i].distance >= nearest.distance) continue;
                nearest = hits[i];
                found = true;
            }
            return found;
        }

        // ---------- Held model and placeholder visuals ----------

        /// <summary>
        /// Puts a copy of the held-model prefab in the player's hand. Swings rotate it around its grip,
        /// and the charge orb and bolts start at its MuzzleAnchor.
        /// </summary>
        private void BuildHeldModel()
        {
            restPosition = gripPosition;
            toolPivot = new GameObject("Mining Laser Tool").transform;
            toolPivot.SetParent(muzzle, false);
            toolPivot.localPosition = restPosition;

            GameObject model = Instantiate(heldModel, toolPivot, false);
            model.name = heldModel.name + " (held)";
            Transform body = model.transform;
            body.localRotation = Quaternion.Euler(heldModelRotation);
            body.localScale = Vector3.one * heldModelScale;

            // A held weapon must not block the player's movement, the melee checks, or its own bolts.
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Destroy(collider);
            }
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            // Shift the model so its grip lands on the pivot; swings then turn around the hand.
            Transform grip = body.Find("GripAnchor");
            if (grip != null) body.localPosition = -toolPivot.InverseTransformPoint(grip.position);
            else Debug.LogWarning("Held model has no GripAnchor child; holding it by its origin.", this);

            Transform tip = body.Find("MuzzleAnchor");
            Vector3 muzzlePosition;
            if (tip != null)
            {
                muzzlePosition = toolPivot.InverseTransformPoint(tip.position);
            }
            else
            {
                Debug.LogWarning("Held model has no MuzzleAnchor child; shots start in front of the grip.", this);
                muzzlePosition = body.localRotation * new Vector3(0f, 0f, 0.3f);
            }
            // The pivot sits at the grip, so the muzzle's distance from it is roughly the barrel length.
            BuildMuzzle(muzzlePosition, body.localRotation, muzzlePosition.magnitude);
        }

        private void BuildPlaceholderTool()
        {
            restPosition = Vector3.zero;
            toolPivot = new GameObject("Mining Laser Tool (placeholder)").transform;
            toolPivot.SetParent(muzzle, false);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null)
            {
                toolMaterial = new Material(lit) { name = "Mining Laser Tool (runtime)" };
                toolMaterial.SetColor("_BaseColor", new Color(0.25f, 0.27f, 0.3f));
            }

            Transform body = VisualPrimitive(PrimitiveType.Cube, "Body", toolMaterial).transform;
            body.SetParent(toolPivot, false);
            body.localPosition = new Vector3(0f, 0f, 0.2f);
            body.localScale = new Vector3(0.05f, 0.05f, 0.4f);

            BuildMuzzle(new Vector3(0f, 0f, 0.43f), Quaternion.identity, 0.4f);
        }

        /// <summary>
        /// Creates the point bolts launch from and the charge electricity on it. The point's -Z faces back
        /// along the barrel so arcs can crawl down it.
        /// </summary>
        private void BuildMuzzle(Vector3 localPosition, Quaternion localRotation, float barrelLength)
        {
            muzzlePoint = new GameObject("Muzzle Point").transform;
            muzzlePoint.SetParent(toolPivot, false);
            muzzlePoint.localPosition = localPosition;
            muzzlePoint.localRotation = localRotation;

            energyMaterial = UnlitMaterial("Laser Energy (runtime)", energyColor);
            energyCoreMaterial = UnlitMaterial("Laser Energy Core (runtime)", Color.Lerp(energyColor, Color.white, 0.6f));
            energyFullMaterial = UnlitMaterial("Laser Energy Full (runtime)", Color.Lerp(energyColor, Color.white, 0.45f));

            chargeEffect = muzzlePoint.gameObject.AddComponent<ChargeEffect>();
            chargeEffect.Build(energyMaterial, energyFullMaterial, energyCoreMaterial, barrelLength);
        }

        /// <summary>A plain glowing colour. Falls back to the beam's material if the URP Unlit shader is missing.</summary>
        private Material UnlitMaterial(string materialName, Color color)
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) return new Material(beam.sharedMaterial) { name = materialName };
            var material = new Material(unlit) { name = materialName };
            material.SetColor("_BaseColor", color);
            return material;
        }

        /// <summary>A primitive with no collider and no shadows, for view-model and effect visuals.</summary>
        internal static GameObject VisualPrimitive(PrimitiveType type, string name, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name;
            Collider collider = item.GetComponent<Collider>();
            collider.enabled = false; // disabled now so it cannot be hit this frame; removed at end of frame
            Destroy(collider);
            var renderer = item.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return item;
        }

        private void UpdateVisuals()
        {
            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime / 0.15f);
            fullChargePop = Mathf.MoveTowards(fullChargePop, 0f, Time.deltaTime / 0.2f);
            Vector3 position = new Vector3(0f, 0f, -0.1f * recoil);
            Quaternion rotation = Quaternion.identity;
            beam.enabled = false;

            if (IsSwinging)
            {
                StrikePose(CurrentStrike(), Mathf.Clamp01(swingElapsed / swingLength), out Vector3 offset, out Vector3 angles);
                position += offset;
                rotation = Quaternion.Euler(angles);
            }
            else if (IsCharging)
            {
                float charge = Charge01;
                position.z -= 0.06f * charge;
                position.z += 0.05f * fullChargePop * fullChargePop; // jolt forward the moment it is full
                if (charge >= 1f) position += Random.insideUnitSphere * 0.005f; // slight shake while full
                rotation = Quaternion.Euler(-4f, -8f, 0f); // point toward the crosshair
            }

            if (IsSwinging)
            {
                // Strikes are timed exactly, so follow the pose with no smoothing.
                basePosition = restPosition + position;
                baseRotation = rotation;
            }
            else
            {
                // Ease in and out of the charging pose instead of snapping.
                float follow = 1f - Mathf.Exp(-25f * Time.deltaTime);
                basePosition = Vector3.Lerp(basePosition, restPosition + position, follow);
                baseRotation = Quaternion.Slerp(baseRotation, rotation, follow);
            }

            UpdateMotion(out Vector3 motionOffset, out Vector3 motionAngles);
            toolPivot.localPosition = basePosition + motionOffset;
            toolPivot.localRotation = Quaternion.Euler(motionAngles) * baseRotation;

            kick = Vector3.Lerp(kick, Vector3.zero, 1f - Mathf.Exp(-14f * Time.deltaTime));

            chargeEffect.Show(IsCharging, Charge01);
        }

        /// <summary>
        /// Sway, bob, landing dip and idle breathing for the held laser. Strikes damp all of it so their poses stay
        /// crisp; charging damps the bob so aiming stays steady.
        /// </summary>
        private void UpdateMotion(out Vector3 offset, out Vector3 angles)
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            // Sway: the laser lags behind how fast the view is turning, then catches up.
            Quaternion aim = aimOrigin.rotation;
            Vector3 turn = (Quaternion.Inverse(lastAim) * aim).eulerAngles;
            lastAim = aim;
            float pitchSpeed = Mathf.DeltaAngle(0f, turn.x) / dt;
            float yawSpeed = Mathf.DeltaAngle(0f, turn.y) / dt;
            var swayTarget = new Vector2(
                Mathf.Clamp(-pitchSpeed * 0.012f, -5f, 5f),
                Mathf.Clamp(-yawSpeed * 0.012f, -6f, 6f)) * swayStrength;
            sway = Vector2.Lerp(sway, swayTarget, 1f - Mathf.Exp(-10f * dt));

            // Bob: a figure-eight in step with walking speed; sprinting makes it faster and bigger.
            float speed = 0f, verticalSpeed = 0f;
            bool grounded = true;
            if (body != null)
            {
                Vector3 velocity = body.velocity;
                verticalSpeed = velocity.y;
                velocity.y = 0f;
                speed = velocity.magnitude;
                grounded = body.isGrounded;
            }
            float targetWeight = grounded ? Mathf.Clamp(speed / 4f, 0f, 1.75f) : 0f;
            bobWeight = Mathf.Lerp(bobWeight, targetWeight, 1f - Mathf.Exp(-8f * dt));
            if (grounded) bobPhase += dt * Mathf.Max(speed, 0.5f) * 1.6f;
            float side = Mathf.Sin(bobPhase);
            float dip = (Mathf.Cos(bobPhase * 2f) - 1f) * 0.5f; // 0 to -1, twice per cycle: once per step

            // Landing: a quick dip that springs back. In the air the laser floats up slightly while falling.
            // Only after a real fall: brief ground-contact flicker on slopes and steps must not trigger it.
            if (grounded && !wasGrounded && airTime > 0.15f)
                landDipTarget = Mathf.Clamp(-lastVerticalSpeed * 0.006f, 0f, 0.05f);
            airTime = grounded ? 0f : airTime + dt;
            wasGrounded = grounded;
            lastVerticalSpeed = verticalSpeed;
            landDip = Mathf.Lerp(landDip, landDipTarget, 1f - Mathf.Exp(-30f * dt));
            landDipTarget = Mathf.Lerp(landDipTarget, 0f, 1f - Mathf.Exp(-9f * dt));
            float liftTarget = grounded ? 0f : Mathf.Clamp(-verticalSpeed * 0.002f, -0.015f, 0.02f);
            airLift = Mathf.Lerp(airLift, liftTarget, 1f - Mathf.Exp(-8f * dt));

            // Breathing: a slow drift that fades out once you move.
            float idle = Mathf.Clamp01(1f - bobWeight * 2f);
            float breath = Mathf.Sin(Time.time * 1.6f) * idle;

            float strikeDamp = IsSwinging ? 0.35f : 1f;
            float bob = bobStrength * strikeDamp * (IsCharging ? 0.4f : 1f);
            float swayScale = strikeDamp;

            offset = new Vector3(
                side * 0.012f * bobWeight * bob + sway.y * 0.002f * swayScale,
                (dip * 0.012f * bobWeight + breath * 0.002f) * bob - sway.x * 0.0015f * swayScale
                    + (airLift - landDip) * bobStrength * strikeDamp,
                0f);
            angles = new Vector3(
                sway.x * swayScale + (dip * 0.6f * bobWeight + breath * 0.3f) * bob + landDip * 40f * bobStrength * strikeDamp,
                sway.y * swayScale,
                -sway.y * 0.5f * swayScale + side * 1.2f * bobWeight * bob);
        }

        /// <summary>Jab and bash alternate; the last swing of the combo is always the slam.</summary>
        private Strike CurrentStrike()
        {
            if (comboStep == ComboLength - 1 && ComboLength > 1) return Slam;
            return comboStep % 2 == 0 ? Jab : Bash;
        }

        /// <summary>
        /// Pose at fraction t of a swing: ease into the wind-up, accelerate into the strike, hold it briefly,
        /// then settle back to the resting hold.
        /// </summary>
        private static void StrikePose(Strike strike, float t, out Vector3 position, out Vector3 angles)
        {
            float hit = strike.HitAt;
            float windupEnd = hit * 0.7f;
            float holdEnd = Mathf.Min(0.9f, hit + StrikeHold);

            if (t < windupEnd)
            {
                float k = 1f - Mathf.Pow(1f - t / windupEnd, 2f); // fast start, slow into the wind-up
                position = Vector3.Lerp(Vector3.zero, strike.WindupPosition, k);
                angles = Vector3.Lerp(Vector3.zero, strike.WindupRotation, k);
            }
            else if (t < hit)
            {
                float k = (t - windupEnd) / (hit - windupEnd);
                k *= k; // accelerate into the impact
                position = Vector3.Lerp(strike.WindupPosition, strike.StrikePosition, k);
                angles = Vector3.Lerp(strike.WindupRotation, strike.StrikeRotation, k);
            }
            else if (t < holdEnd)
            {
                position = strike.StrikePosition;
                angles = strike.StrikeRotation;
            }
            else
            {
                float k = Mathf.SmoothStep(0f, 1f, (t - holdEnd) / (1f - holdEnd));
                position = Vector3.Lerp(strike.StrikePosition, Vector3.zero, k);
                angles = Vector3.Lerp(strike.StrikeRotation, Vector3.zero, k);
            }
        }

        // ---------- Camera kick ----------
        // Applied only while the player camera renders and undone straight after, so it never changes where
        // the player is aiming and never fights the camera rotation set by FirstPersonController.

        private void AddKick(Vector3 degrees) => kick += degrees * viewKick;

        private void ApplyKick(ScriptableRenderContext context, Camera rendering)
        {
            if (rendering != viewCamera || viewCamera == null || kick.sqrMagnitude < 0.0001f) return;
            unkickedRotation = aimOrigin.localRotation;
            aimOrigin.localRotation = unkickedRotation * Quaternion.Euler(kick);
            kickApplied = true;
        }

        private void RemoveKick(ScriptableRenderContext context, Camera rendering)
        {
            if (!kickApplied || rendering != viewCamera) return;
            aimOrigin.localRotation = unkickedRotation;
            kickApplied = false;
        }
    }
}
