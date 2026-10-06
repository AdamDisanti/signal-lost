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
        private bool captured;

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

        private void SetCapture(bool capture)
        {
            captured = capture;
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }

        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                SetCapture(false);
            else if (!captured && Application.isFocused &&
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

            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            if (canControl && controller.isGrounded && jump.WasPressedThisFrame())
                verticalSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);
            verticalSpeed -= gravity * Time.deltaTime;

            Vector2 axis = canControl ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
            float speed = canControl && sprint.IsPressed() ? sprintSpeed : walkSpeed;
            Vector3 velocity = (transform.right * axis.x + transform.forward * axis.y) * speed;
            velocity.y = verticalSpeed;
            CollisionFlags collisions = controller.Move(velocity * Time.deltaTime);
            if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
            if ((collisions & CollisionFlags.Below) != 0 && verticalSpeed < 0f) verticalSpeed = -2f;
        }
    }
}
