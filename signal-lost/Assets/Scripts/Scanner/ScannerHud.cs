using UnityEngine;
using UnityEngine.UI;

namespace SignalLost.Scanner
{
    public sealed class ScannerHud : MonoBehaviour
    {
        [SerializeField] private ScannerController scanner;
        [SerializeField] private ScannerToggle scannerToggle;
        [SerializeField] private CanvasGroup hud;
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
                strengthMeter.value = 0f;
                return;
            }

            strengthMeter.value = scanner.SignalStrength01;

        }
    }
}