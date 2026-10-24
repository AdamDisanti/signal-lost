using SignalLost.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SignalLost.Player
{
    /// <summary>
    /// E (gamepad north): uses the <see cref="IInteractable"/> under the crosshair within <see cref="reach"/> metres.
    /// While a quest popup is open, E advances it instead. <see cref="Prompt"/> is what the HUD shows under the
    /// crosshair. Walls block the ray, so nobody can be talked to through them.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [Tooltip("The camera the interaction ray starts from. Empty: the player's camera.")]
        [SerializeField] private Camera view;
        [SerializeField, Min(0.5f)] private float reach = 3f;

        private InputActionAsset runtimeActions;
        private InputAction interact;
        private PlayerLife life;

        public PlayerGear Gear { get; private set; }
        public PlayerFlask Flask { get; private set; }
        public PlayerLife Life => life;
        public Transform Root => transform;
        /// <summary>The thing under the crosshair that can be used right now, or null.</summary>
        public IInteractable Target { get; private set; }
        /// <summary>"E  Talk to Cavendish", or empty.</summary>
        public string Prompt => Target != null && !string.IsNullOrEmpty(Target.Prompt) && !UiMenus.AnyOpen && !CardWaiting ? "E  " + Target.Prompt : "";

        // E dismisses an open item card first, so no interaction prompt while one is up.
        private static bool CardWaiting => GameplayOverlay.Current != null && GameplayOverlay.Current.CardOpen;

        private void Awake()
        {
            Gear = GetComponent<PlayerGear>();
            Flask = GetComponent<PlayerFlask>();
            life = GetComponent<PlayerLife>();
            if (view == null) view = GetComponentInChildren<Camera>();
            if (inputActions != null)
            {
                runtimeActions = Instantiate(inputActions);
                InputActionMap map = runtimeActions.FindActionMap("Player", true);
                map.bindingMask = InputBinding.MaskByGroups("Keyboard&Mouse", "Gamepad");
                interact = map.FindAction("Interact", true);
            }
        }

        private void OnEnable() => interact?.actionMap.Enable();
        private void OnDisable() => interact?.actionMap.Disable();

        private void OnDestroy()
        {
            if (runtimeActions != null) Destroy(runtimeActions);
        }

        private void Start() => DialogueUi.Instance?.BindPlayer(transform);

        private void Update()
        {
            Target = Busy ? null : FindTarget();
            bool controllable = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
            if (controllable && !UiMenus.AnyOpen && !UiMenus.ChangedThisFrame && interact != null && interact.WasPressedThisFrame())
                TryInteract();
        }

        private bool Busy => (life != null && (life.IsRespawning || life.IsDead));

        /// <summary>Advances an open popup, dismisses an item card, or uses what is under the crosshair. Returns true if something happened.</summary>
        public bool TryInteract()
        {
            if (Busy) return false;
            DialogueUi dialogue = DialogueUi.Instance;
            if (dialogue != null && dialogue.PopupOpen) return dialogue.Advance();
            // A new-gear card waiting to be read takes the next E.
            GameplayOverlay overlay = GameplayOverlay.Current;
            if (overlay != null && overlay.CardOpen) return overlay.DismissCard();
            IInteractable target = FindTarget();
            return target != null && target.Interact(this);
        }

        private IInteractable FindTarget()
        {
            if (view == null || !Physics.Raycast(view.transform.position, view.transform.forward, out RaycastHit hit, reach, ~0,
                    QueryTriggerInteraction.Ignore)) return null;
            var target = hit.collider.GetComponentInParent<IInteractable>();
            return target != null && target.CanInteract(this) ? target : null;
        }
    }
}
