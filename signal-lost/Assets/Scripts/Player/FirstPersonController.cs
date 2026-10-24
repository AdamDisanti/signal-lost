using UnityEngine;
using UnityEngine.InputSystem;

namespace SignalLost.Player
{
    /// <summary>
    /// First-person walking, sprinting, jumping and looking, plus crouch and slide (meeting notes 10-23):
    /// tap C to crouch or stand; hold C while sprinting to slide, a burst of speed that bleeds off. Jumping out of a
    /// slide (slide cancel) keeps its speed in the air, and landing with C still held slides again, so chaining
    /// slide-jumps is faster than sprinting.
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
        public bool IsCrouched => crouched;
        public bool IsSliding => sliding;

        /// <summary>Look-speed multiplier from the settings menu (GameSettings.LookSensitivity).</summary>
        public static float SensitivityScale { get; set; } = 1f;

        public float Impairment { get; set; }
        public float MovementScale => 1f - (0.55f * 2f / 3f) * Mathf.Clamp01(Impairment);

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
            if (inputActions == null || cameraPivot == null)
            {
                Debug.LogError("FirstPersonController requires input actions and a camera pivot.", this);
                enabled = false;
                return;
            }

            // Each player owns its action state; never enable/disable the shared source asset.
            runtimeActions = Instantiate(inputActions);
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
            bool grounded = controller.isGrounded;
            float dt = Time.deltaTime;
            if (grounded && verticalSpeed < 0f) verticalSpeed = -2f;

            Vector2 axis = canMove ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
            // Only stagger while moving; a drunk player can still stop safely near cliffs.
            float stagger = Mathf.Sin(Time.time * 2.1f) * Mathf.Clamp01(Impairment) * 0.45f;
            axis = new Vector2(axis.x * Mathf.Cos(stagger) + axis.y * Mathf.Sin(stagger),
                axis.y * Mathf.Cos(stagger) - axis.x * Mathf.Sin(stagger));
            bool sprinting = canMove && sprint.IsPressed() && axis.y > 0.1f;
            Vector3 wish = transform.right * axis.x + transform.forward * axis.y;

            // C: hold while sprinting (or landing from a slide-jump) to slide; otherwise a tap toggles crouching.
            bool crouchHeld = canMove && crouch.IsPressed();
            if (canMove && crouch.WasPressedThisFrame())
            {
                if (grounded && (sprinting || momentum.magnitude > sprintSpeed)) StartSlide(wish);
                else if (crouched) TryStand();
                else Crouch(true);
            }
            else if (!sliding && grounded && crouchHeld && momentum.magnitude > sprintSpeed * MovementScale) StartSlide(wish);

            if (canMove && grounded && jump.WasPressedThisFrame() && (sliding || !crouched || TryStand()))
            {
                verticalSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);
                if (sliding)
                {
                    // Slide cancel: keep the slide's speed into the jump.
                    momentum *= slideJumpCarry;
                    sliding = false;
                    TryStand();
                }
            }
            verticalSpeed -= gravity * dt;

            float baseSpeed = (crouched && !sliding ? crouchSpeed : sprinting ? sprintSpeed : walkSpeed) * MovementScale;
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
