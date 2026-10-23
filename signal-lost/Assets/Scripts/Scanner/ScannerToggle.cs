using UnityEngine;
using UnityEngine.InputSystem;

namespace SignalLost.Scanner
{
    public sealed class ScannerToggle : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private GameObject scannerVisual;

        private InputActionAsset runtimeActions;
        private InputActionMap playerMap;
        private InputAction toggleScanner;

        public bool IsRaised { get; private set; }
        /// <summary>The scanner model and display shown while raised (hands attach here).</summary>
        public GameObject Visual => scannerVisual;

        private void Awake()
        {
            if (inputActions == null || scannerVisual == null)
            {
                Debug.LogError("ScannerToggle requires input actions and scanner visual.", this);
                enabled = false;
                return;
            }

            runtimeActions = Instantiate(inputActions);
            playerMap = runtimeActions.FindActionMap("Player", true);
            playerMap.bindingMask = InputBinding.MaskByGroups("Keyboard&Mouse", "Gamepad");
            toggleScanner = playerMap.FindAction("ToggleScanner", true);

            SetRaised(false);
        }

        private void OnEnable() => playerMap?.Enable();

        private void OnDisable()
        {
            playerMap?.Disable();
            SetRaised(false);
        }

        private void OnDestroy()
        {
            if (runtimeActions != null)
            {
                Destroy(runtimeActions);
            }
        }

        private void Update()
        {
            if (!Application.isFocused || Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }
            if (toggleScanner.WasPressedThisFrame())
            {
                SetRaised(!IsRaised);
            }
        }

        private void SetRaised(bool raised)
        {
            IsRaised = raised;
            if (scannerVisual != null)
            {
                scannerVisual.SetActive(raised);
            }
        }
    }
}