using UnityEngine;
using UnityEngine.InputSystem;

namespace SignalLost.Player
{
    /// <summary>
    /// First-person walking, sprinting, jumping and looking, plus crouch and slide (meeting notes 10-23):
    /// tap C to crouch or stand; hold C while sprinting to slide, a burst of speed that bleeds off. Jumping out of a
    /// slide (slide cancel) keeps its speed in the air, and landing with C still held slides again, so chaining
    /// slide-jumps is faster than sprinting. Upgrades and rage add speed and jump height (<see cref="SetSpeedBonus"/>,
    /// <see cref="JumpHeightScale"/>), Moon Shoes III a directional double jump, and the knuckles a magnet
    /// <see cref="Lunge"/> onto enemies.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Transform cameraPivot;
        [SerializeField, Min(0.1f)] private float walkSpeed = 4f;
        [SerializeField, Min(0.1f)] private float sprintSpeed = 7f;
        [SerializeField, Min(0.1f)] private float jumpHeight = 1.2f;
        [SerializeField, Min(0.1f)] private float gravity = 20f;
        [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.12f;
        [SerializeField, Min(1f)] private float stickSensitivity = 150f;
        [SerializeField, Range(1f, 89f)] private float pitchLimit = 85f;

        [Header("Crouch and slide")]
        [SerializeField, Min(0.1f)] private float crouchSpeed = 2.2f;
        [SerializeField, Min(0.5f)] private float crouchHeight = 1.1f;
        [SerializeField, Min(0.1f)] private float crouchEyeHeight = 0.95f;
        [Tooltip("Speed at the start of a slide, as a multiple of the sprint speed.")]
        [SerializeField, Min(1f)] private float slideBoost = 1.5f;
        [Tooltip("How fast a slide slows down (m/s per second). Lower slides further.")]
        [SerializeField, Min(0.1f)] private float slideFriction = 7f;
        [Tooltip("The slide ends (into a crouch) below this speed.")]
        [SerializeField, Min(0.1f)] private float slideEndSpeed = 3f;
        [Tooltip("Speed kept when jumping out of a slide.")]
        [SerializeField, Range(0.5f, 1.5f)] private float slideJumpCarry = 1.05f;
        [Tooltip("How quickly air momentum fades back to normal running speed (per second).")]
        [SerializeField, Min(0f)] private float airDrag = 0.6f;

        [Header("Running arm swing (sprinting)")]
        [Tooltip("Degrees each arm swings forward and back about the shoulder.")]
        [SerializeField, Range(0f, 60f)] private float armSwingReach = 36f;
        [Tooltip("Degrees the hand crosses in toward the chest at the front of the swing.")]
        [SerializeField, Range(0f, 40f)] private float armSwingAcross = 16f;
        [Tooltip("How wide the loop is: in on the way forward, out on the way back (degrees). 0 is a straight line.")]
        [SerializeField, Range(0f, 25f)] private float armSwingLoop = 9f;
        [Tooltip("Arm swings per metre run (radians). Lower is a slower, longer stride.")]
        [SerializeField, Range(0.5f, 3f)] private float strideRate = 1.6f;

        private CharacterController controller;
        private InputActionAsset runtimeActions;
        private InputActionMap playerMap;
        private InputAction move, look, jump, sprint;
        private float verticalSpeed, pitch, stunnedUntil;
        private bool captured;
        private InputAction crouch;
        private bool crouched, sliding;
        private Vector3 momentum;          // horizontal velocity carried by slides and slide-jumps
        private float standHeight, standEyeHeight, eyeHeight;
        private Vector3 standCenter;

        /// <summary>Upgrades (Slick Kneepads) multiply how far a slide goes.</summary>
        public float SlideDistanceScale { get; set; } = 1f;
        /// <summary>Upgrades (Moon Shoes) multiply how high a jump goes.</summary>
        public float JumpHeightScale { get; set; } = 1f;
        /// <summary>Moon Shoes III: one more jump in mid-air, launched the way the movement keys point.</summary>
        public bool DoubleJump { get; set; }
        /// <summary>Raised on a double jump, with where the feet were.</summary>
        public event System.Action<Vector3> DoubleJumped;

        private readonly System.Collections.Generic.Dictionary<object, float> speedBonuses = new System.Collections.Generic.Dictionary<object, float>();

        /// <summary>Walking and sprinting speed multiplier from every bonus together (Moon Shoes, the knuckles, rage).</summary>
        public float SpeedBonus { get; private set; } = 1f;

        /// <summary>Sets one source's speed multiplier (1 removes it); they multiply together into <see cref="SpeedBonus"/>.</summary>
        public void SetSpeedBonus(object key, float multiplier)
        {
            if (Mathf.Approximately(multiplier, 1f)) speedBonuses.Remove(key);
            else speedBonuses[key] = multiplier;
            float total = 1f;
            foreach (float bonus in speedBonuses.Values) total *= bonus;
            SpeedBonus = total;
        }

        private Vector3 lungeVelocity;
        private float lungeUntil;
        private bool airJumped;

        /// <summary>True while a <see cref="Lunge"/> is carrying the player.</summary>
        public bool IsLunging => Time.time < lungeUntil;

        /// <summary>
        /// Carries the player at <paramref name="velocity"/> for <paramref name="seconds"/> with no gravity or steering
        /// (the knuckles' magnet lunge onto an enemy). Ends early on hitting a wall, or with <see cref="EndLunge"/>.
        /// </summary>
        public void Lunge(Vector3 velocity, float seconds)
        {
            lungeVelocity = velocity;
            lungeUntil = Time.time + seconds;
            sliding = false;
            if (crouched) TryStand();
        }

        /// <summary>Stops a lunge, keeping a little of its speed so it doesn't feel like hitting glue.</summary>
        public void EndLunge()
        {
            if (lungeUntil <= 0f) return;
            lungeUntil = 0f;
            momentum = new Vector3(lungeVelocity.x, 0f, lungeVelocity.z) * 0.25f;
            verticalSpeed = Mathf.Min(verticalSpeed, 0f);
        }
        public bool IsCrouched => crouched;
        public bool IsSliding => sliding;
        public bool IsGrounded => controller != null && controller.isGrounded;
        public float VerticalSpeed => verticalSpeed;

        /// <summary>
        /// Asked when C is pressed in mid-air (the jump smash, <see cref="SignalLost.Combat.JumpSmash"/>). Returning true
        /// uses the press, so the player does not also crouch.
        /// </summary>
        public System.Func<bool> AirCrouchPressed { get; set; }

        /// <summary>Drives the player straight down at <paramref name="speed"/>, keeping a little sideways drift.</summary>
        public void Dive(float speed)
        {
            verticalSpeed = Mathf.Min(verticalSpeed, -speed);
            momentum *= 0.25f;
        }

        /// <summary>Sprinting on the ground (not sliding or crouched): the arms pump.</summary>
        public bool IsRunning { get; private set; }
        /// <summary>Radians; advances with ground speed. sin(StridePhase) swings the right arm forward and back.</summary>
        public float StridePhase { get; private set; }

        /// <summary>Camera-space right shoulder (the left mirrors x): the running arm swings about it.</summary>
        public static readonly Vector3 RightShoulder = new Vector3(0.2f, -0.32f, 0f);

        /// <summary>
        /// The running arm swing for one arm (<paramref name="side"/> 1 right, -1 left, opposite strides), as a turn
        /// about that shoulder: a wide loop that comes up and across toward the chest, then sweeps back down and out
        /// of view on the outside. Applied to the whole hand (position about the shoulder), so it reads as the arm
        /// swinging, not the wrist.
        /// </summary>
        public Quaternion ArmSwing(float side)
        {
            float forward = side * Mathf.Sin(StridePhase);   // 1 at the front of the swing, -1 at the back
            float rising = side * Mathf.Cos(StridePhase);    // > 0 on the way forward
            float pitch = 8f - armSwingReach * forward;                              // up to the chest, down past the hip
            float across = 6f + armSwingAcross * forward + armSwingLoop * rising;    // inside lane up, outside lane down
            return Quaternion.Euler(pitch, -side * across, side * (4f + 8f * forward));
        }

        /// <summary>Look-speed multiplier from the settings menu (GameSettings.LookSensitivity).</summary>
        public static float SensitivityScale { get; set; } = 1f;

        public float Impairment { get; set; }
        public float MovementScale => 1f - (0.55f * 2f / 3f) * Mathf.Clamp01(Impairment);

        /// <summary>The move stick or WASD right now (x = right, y = forward), for the dodge's direction.</summary>
        public Vector2 MoveInput => move != null && enabled ? move.ReadValue<Vector2>() : Vector2.zero;

        /// <summary>True while stunned (see <see cref="Stun"/>): no walking or jumping, but looking and gravity still work.</summary>
        public bool IsStunned => Time.time < stunnedUntil;

        /// <summary>Roots the player to the spot for a few seconds (the Chief Toad's scream). Use PlayerStun for the full effect.</summary>
        public void Stun(float seconds) => stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);

        public void ResetMotion()
        {
            verticalSpeed = pitch = 0f;
            momentum = Vector3.zero;
            sliding = false;
            if (crouched) TryStand();
            cameraPivot.localRotation = Quaternion.identity;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (GetComponent<PlayerDodge>() == null) gameObject.AddComponent<PlayerDodge>(); // Left Alt (docs/desert-bosses.md)
            if (inputActions == null || cameraPivot == null)
            {
                Debug.LogError("FirstPersonController requires input actions and a camera pivot.", this);
                enabled = false;
                return;
            }

            // Each player owns its action state; never enable/disable the shared source asset.
            runtimeActions = Instantiate(inputActions); SignalLost.Core.KeyBindings.Register(runtimeActions);
            playerMap = runtimeActions.FindActionMap("Player", true);
            playerMap.bindingMask = InputBinding.MaskByGroups("Keyboard&Mouse", "Gamepad");
            move = playerMap.FindAction("Move", true);
            look = playerMap.FindAction("Look", true);
            jump = playerMap.FindAction("Jump", true);
            sprint = playerMap.FindAction("Sprint", true);
            crouch = playerMap.FindAction("Crouch", true);
            standHeight = controller.height;
            standCenter = controller.center;
            standEyeHeight = eyeHeight = cameraPivot.localPosition.y;
        }

        private void OnEnable()
        {
            if (playerMap == null) return;
            playerMap.Enable();
            SetCapture(true);
        }

        private void OnDisable()
        {
            playerMap?.Disable();
            SetCapture(false);
        }

        private void OnDestroy()
        {
            if (runtimeActions != null) Destroy(runtimeActions);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SetCapture(false);
        }

        private bool menuOpen;
        private int menuChangedFrame = -1;

        public void SetMenuOpen(bool open)
        {
            menuOpen = open;
            menuChangedFrame = Time.frameCount;
            SetCapture(!open && Application.isFocused);
        }

        private void SetCapture(bool capture)
        {
            captured = capture;
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }

        private void Update()
        {
            if (menuOpen)
                SetCapture(false);
            else if (menuChangedFrame != Time.frameCount && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                SetCapture(false);
            else if (menuChangedFrame != Time.frameCount && !captured && Application.isFocused &&
                     Mouse.current?.leftButton.wasPressedThisFrame == true)
                SetCapture(true);

            bool canControl = captured && Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
            if (canControl)
            {
                Vector2 delta = look.ReadValue<Vector2>();
                // Mouse delta is already measured per frame; sticks are angular rates.
                float sensitivity = (look.activeControl?.device is Mouse
                    ? mouseSensitivity : stickSensitivity * Time.deltaTime) * SensitivityScale;
                transform.Rotate(0f, delta.x * sensitivity, 0f);
                pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -pitchLimit, pitchLimit);
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            bool canMove = (canControl || (menuOpen && Application.isFocused)) && !IsStunned;

            // Testing tools: cheat mode (End: 9999 damage and free flight) and stage skip (arrows: next / previous stage).
            if (canControl && SignalLost.Core.KeyBindings.WasPressed("cheat"))
            {
                SignalLost.Core.CheatMode.Toggle();
                SignalLost.Enemies.Desert.MiniBossHud.Message(SignalLost.Core.CheatMode.Enabled
                        ? "TESTING TOOL: CHEAT MODE ON: 9999 damage, flying (" + SignalLost.Core.KeyBindings.Display("jump") + " up, " + SignalLost.Core.KeyBindings.Display("crouch") + " down)"
                        : "TESTING TOOL: CHEAT MODE OFF", new Color(1f, 0.85f, 0.3f), 2.5f);
            }
            if (canControl && SignalLost.Core.KeyBindings.WasPressed("stage_next")) SignalLost.Progression.StageSkip.Go(1);
            else if (canControl && SignalLost.Core.KeyBindings.WasPressed("stage_previous")) SignalLost.Progression.StageSkip.Go(-1);
            if (SignalLost.Core.CheatMode.Enabled)
            {
                Fly(canMove);
                return;
            }
            if (!controller.enabled)
            {
                // Back from flying: walk (and fall) again from here.
                controller.enabled = true;
                verticalSpeed = 0f;
                momentum = Vector3.zero;
            }

            bool grounded = controller.isGrounded;
            float dt = Time.deltaTime;
            if (grounded && verticalSpeed < 0f) verticalSpeed = -2f;
            if (grounded) airJumped = false;
            if (lungeUntil > 0f && !IsLunging) EndLunge();

            Vector2 axis = canMove ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
            // Only stagger while moving; a drunk player can still stop safely near cliffs.
            float stagger = Mathf.Sin(Time.time * 2.1f) * Mathf.Clamp01(Impairment) * 0.45f;
            axis = new Vector2(axis.x * Mathf.Cos(stagger) + axis.y * Mathf.Sin(stagger),
                axis.y * Mathf.Cos(stagger) - axis.x * Mathf.Sin(stagger));
            bool sprinting = canMove && sprint.IsPressed() && axis.y > 0.1f;
            IsRunning = sprinting && grounded && !sliding && !crouched;
            // One stride cycle (two steps) per ~1.6 m at sprint pace; drives the running arm swing.
            if (grounded) StridePhase += dt * new Vector2(controller.velocity.x, controller.velocity.z).magnitude * strideRate;
            Vector3 wish = transform.right * axis.x + transform.forward * axis.y;

            // C: hold while sprinting (or landing from a slide-jump) to slide; otherwise a tap toggles crouching.
            bool crouchHeld = canMove && crouch.IsPressed();
            if (canMove && crouch.WasPressedThisFrame())
            {
                bool usedInAir = !grounded && AirCrouchPressed != null && AirCrouchPressed(); // the jump smash took it
                if (usedInAir) { }
                else if (grounded && (sprinting || momentum.magnitude > sprintSpeed)) StartSlide(wish);
                else if (crouched) TryStand();
                else Crouch(true);
            }
            else if (!sliding && grounded && crouchHeld && momentum.magnitude > sprintSpeed * MovementScale) StartSlide(wish);

            if (canMove && grounded && jump.WasPressedThisFrame() && (sliding || !crouched || TryStand()))
            {
                verticalSpeed = Mathf.Sqrt(2f * gravity * jumpHeight * JumpHeightScale);
                if (sliding)
                {
                    // Slide cancel: keep the slide's speed into the jump.
                    momentum *= slideJumpCarry;
                    sliding = false;
                    TryStand();
                }
            }
            else if (canMove && !grounded && jump.WasPressedThisFrame()) AirJump(wish);
            verticalSpeed -= gravity * dt;

            float baseSpeed = (crouched && !sliding ? crouchSpeed : (sprinting ? sprintSpeed : walkSpeed) * SpeedBonus) * MovementScale;
            Vector3 velocity;
            if (sliding)
            {
                float speed = Mathf.Max(0f, momentum.magnitude - slideFriction / Mathf.Max(0.1f, SlideDistanceScale) * dt);
                // Slight steering, no acceleration.
                Vector3 direction = Vector3.Slerp(momentum.normalized, wish.sqrMagnitude > 0.01f ? wish.normalized : momentum.normalized, 1.5f * dt);
                momentum = direction * speed;
                velocity = momentum;
                if (speed < slideEndSpeed || !crouchHeld)
                {
                    sliding = false;
                    if (!crouchHeld) TryStand();
                }
            }
            else
            {
                Vector3 target = wish * baseSpeed;
                if (grounded && verticalSpeed <= 0f)
                    momentum = Vector3.MoveTowards(momentum, target, 30f * dt); // normal footing: snappy
                else
                {
                    // In the air, extra speed from a slide-jump fades slowly; input can still steer.
                    float carried = Mathf.Max(momentum.magnitude * (1f - airDrag * dt), baseSpeed);
                    Vector3 steered = Vector3.MoveTowards(momentum, target.sqrMagnitude > 0.01f ? target.normalized * carried : momentum, 12f * dt);
                    momentum = Vector3.ClampMagnitude(steered, carried);
                }
                velocity = momentum;
            }
            if (IsLunging)
            {
                momentum = new Vector3(lungeVelocity.x, 0f, lungeVelocity.z);
                verticalSpeed = lungeVelocity.y;
                velocity = lungeVelocity;
            }
            velocity.y = verticalSpeed;

            // Eyes follow the stance smoothly.
            float eyeTarget = sliding ? crouchEyeHeight - 0.15f : crouched ? crouchEyeHeight : standEyeHeight;
            eyeHeight = Mathf.MoveTowards(eyeHeight, eyeTarget, 6f * dt);
            Vector3 eye = cameraPivot.localPosition;
            cameraPivot.localPosition = new Vector3(eye.x, eyeHeight, eye.z);
            CollisionFlags collisions = controller.Move(velocity * Time.deltaTime);
            if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
            if ((collisions & CollisionFlags.Below) != 0 && verticalSpeed < 0f) verticalSpeed = -2f;
            if ((collisions & CollisionFlags.Sides) != 0 && sliding && controller.velocity.magnitude < slideEndSpeed) sliding = false;
            if ((collisions & CollisionFlags.Sides) != 0 && IsLunging && controller.velocity.magnitude < lungeVelocity.magnitude * 0.3f) EndLunge();
        }

        // Moon Shoes double jump: a fresh jump in mid-air, launched the way the movement keys point right now (straight up
        // with none held), at least at sprinting pace. Once per trip into the air.
        private bool AirJump(Vector3 wish)
        {
            if (!DoubleJump || airJumped || IsLunging || controller.isGrounded) return false;
            airJumped = true;
            verticalSpeed = Mathf.Sqrt(2f * gravity * jumpHeight * JumpHeightScale * 0.9f);
            momentum = wish.sqrMagnitude > 0.01f
                ? wish.normalized * Mathf.Max(new Vector3(momentum.x, 0f, momentum.z).magnitude, sprintSpeed * MovementScale * SpeedBonus)
                : momentum * 0.4f;
            DoubleJumped?.Invoke(transform.position);
            return true;
        }

        private const float FlySpeed = 12f, FlySprintSpeed = 40f;

        // Cheat flight: no gravity and no collisions. Moves where the camera looks; Jump rises, Crouch sinks, Sprint is fast.
        private void Fly(bool canMove)
        {
            if (controller.enabled) controller.enabled = false;
            sliding = false;
            momentum = Vector3.zero;
            verticalSpeed = 0f;
            Vector2 axis = canMove ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
            float rise = canMove ? (jump.IsPressed() ? 1f : 0f) - (crouch.IsPressed() ? 1f : 0f) : 0f;
            Vector3 direction = cameraPivot.forward * axis.y + cameraPivot.right * axis.x + Vector3.up * rise;
            float speed = canMove && sprint.IsPressed() ? FlySprintSpeed : FlySpeed;
            transform.position += Vector3.ClampMagnitude(direction, 1f) * (speed * Time.deltaTime);
            eyeHeight = Mathf.MoveTowards(eyeHeight, standEyeHeight, 6f * Time.deltaTime);
            Vector3 eye = cameraPivot.localPosition;
            cameraPivot.localPosition = new Vector3(eye.x, eyeHeight, eye.z);
        }

        private void OnGUI()
        {
            if (!SignalLost.Core.CheatMode.Enabled) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            style.normal.textColor = new Color(1f, 0.85f, 0.3f);
            GUI.Label(new Rect(12f, 8f, 520f, 24f), "TESTING TOOL: CHEAT MODE (" + SignalLost.Core.KeyBindings.Display("cheat") + " to turn off)", style);
        }

        private void StartSlide(Vector3 wish)
        {
            Vector3 direction = momentum.sqrMagnitude > 1f ? momentum.normalized : wish.sqrMagnitude > 0.01f ? wish.normalized : transform.forward;
            float speed = Mathf.Max(momentum.magnitude, sprintSpeed * MovementScale * slideBoost);
            momentum = direction * speed;
            sliding = true;
            Crouch(true);
        }

        private void Crouch(bool down)
        {
            crouched = down;
            float height = down ? crouchHeight : standHeight;
            controller.height = height;
            controller.center = new Vector3(standCenter.x, standCenter.y - (standHeight - height) * 0.5f, standCenter.z);
        }

        // Stands up if there is room overhead. Returns true if standing.
        private bool TryStand()
        {
            if (!crouched) return true;
            float radius = controller.radius * 0.95f;
            Vector3 bottom = transform.position + Vector3.up * (crouchHeight - radius);
            if (Physics.SphereCast(bottom, radius, Vector3.up, out _, standHeight - crouchHeight + 0.05f, ~0, QueryTriggerInteraction.Ignore))
                return false;
            Crouch(false);
            return true;
        }
    }
}
