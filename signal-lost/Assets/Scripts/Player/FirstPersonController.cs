using SignalLost.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SignalLost.Player
{
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

        private CharacterController controller;
        private InputActionAsset runtimeActions;
        private InputActionMap playerMap;
        private InputAction move, look, jump, sprint;
        private float verticalSpeed, pitch;
        private Vector3 knockbackVelocity;
        private float knockbackDeceleration;
        private bool captured;

        public void ApplyKnockback(Vector3 impulse, float deceleration)
        {
            knockbackVelocity = new Vector3(impulse.x, 0f, impulse.z);
            knockbackDeceleration = deceleration;
            if (impulse.y > 0f) verticalSpeed = Mathf.Max(verticalSpeed, impulse.y);
        }

        public void ClearKnockback()
        {
            knockbackVelocity = Vector3.zero;
            verticalSpeed = 0f;
        }

        public float Impairment { get; set; }
        public float MovementScale => 1f - (0.55f * 2f / 3f) * Mathf.Clamp01(Impairment);

        public void ResetMotion()
        {
            ClearKnockback();
            pitch = 0f;
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
            ClearKnockback();
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

        private void OnControllerColliderHit(ControllerColliderHit hit) =>
            knockbackVelocity = Knockback.SlideAlongWall(knockbackVelocity, hit.normal);

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
                float sensitivity = look.activeControl?.device is Mouse
                    ? mouseSensitivity : stickSensitivity * Time.deltaTime;
                transform.Rotate(0f, delta.x * sensitivity, 0f);
                pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -pitchLimit, pitchLimit);
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            bool canMove = canControl || (menuOpen && Application.isFocused);
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            if (canMove && controller.isGrounded && jump.WasPressedThisFrame())
                verticalSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);
            verticalSpeed -= gravity * Time.deltaTime;

            Vector2 axis = canMove ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
            // Only stagger while moving; a drunk player can still stop safely near cliffs.
            float stagger = Mathf.Sin(Time.time * 2.1f) * Mathf.Clamp01(Impairment) * 0.45f;
            axis = new Vector2(axis.x * Mathf.Cos(stagger) + axis.y * Mathf.Sin(stagger),
                axis.y * Mathf.Cos(stagger) - axis.x * Mathf.Sin(stagger));
            float speed = (canMove && sprint.IsPressed() ? sprintSpeed : walkSpeed) * MovementScale;
            Vector3 velocity = (transform.right * axis.x + transform.forward * axis.y) * speed + knockbackVelocity;
            velocity.y = verticalSpeed;
            CollisionFlags collisions = controller.Move(velocity * Time.deltaTime);
            knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, knockbackDeceleration * Time.deltaTime);
            if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
            if ((collisions & CollisionFlags.Below) != 0 && verticalSpeed < 0f) verticalSpeed = -2f;
        }
    }
}
