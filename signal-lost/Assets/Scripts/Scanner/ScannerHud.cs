using UnityEngine;
using UnityEngine.UI;

namespace SignalLost.Scanner
{
    public sealed class ScannerHud : MonoBehaviour
    {
        [SerializeField] private ScannerController scanner;
        [SerializeField] private ScannerToggle scannerToggle;
        [SerializeField] private Transform compassReference;
        [SerializeField] private CanvasGroup hud;
        [SerializeField] private RectTransform directionArrow;
        [SerializeField] private Slider strengthMeter;

        private void Update()
        {
            bool scannerRaised = scannerToggle != null && scannerToggle.IsRaised;

            if (scannerRaised)
            {
                hud.alpha = 1f;
            }
            else
            {
                hud.alpha = 0f;
            }

            hud.interactable = scannerRaised;
            hud.blocksRaycasts = scannerRaised;

            if (!scannerRaised)
            {
                directionArrow.gameObject.SetActive(false);
                strengthMeter.value = 0f;
                return;
            }

            strengthMeter.value = scanner.SignalStrength01;

            bool hasTarget = scanner.CurrentTarget != null;
            directionArrow.gameObject.SetActive(hasTarget);

            if (!hasTarget)
            {
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(compassReference.forward, Vector3.up);
            Vector3 targetDirection = Vector3.ProjectOnPlane(scanner.DirectionToTarget, Vector3.up);

            if (forward.sqrMagnitude < 0.001f || targetDirection.sqrMagnitude < 0.001f)
            {
                directionArrow.gameObject.SetActive(false);
                return;
            }

            float angle = Vector3.SignedAngle(forward, targetDirection, Vector3.up);
            directionArrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }
    }
}