using UnityEngine;

namespace SignalLost.Scanner
{
    [CreateAssetMenu(menuName = "Signal Lost/Scanner Config")]
    public sealed class ScannerConfig : ScriptableObject
    {
        [Header("Detection")]
        [SerializeField, Min(0.1f)] private float maxRange = 30f;
        [SerializeField, Min(0.02f)] private float updateInterval = 0.2f;

        [Header("Audio")]
        [SerializeField, Min(0.05f)] private float farBeepInterval = 1f;
        [SerializeField, Min(0.05f)] private float closeBeepInterval = 0.15f;
        [SerializeField, Min(0f)] private float minPitch = 0.8f;
        [SerializeField, Min(0f)] private float maxPitch = 1.4f;
        [SerializeField, Range(0f, 1f)] private float minVolume = 0.2f;
        [SerializeField, Range(0f, 1f)] private float maxVolume = 0.8f;

        [Header("Upgrade Multipliers")]
        [SerializeField, Min(0.1f)] private float rangeUpgradeMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float strengthUpgradeMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float audioUpgradeMultiplier = 1f;

        public float MaxRange => maxRange * rangeUpgradeMultiplier;
        public float UpdateInterval => updateInterval;
        public float FarBeepInterval => farBeepInterval;
        public float CloseBeepInterval => closeBeepInterval;
        public float MinPitch => minPitch;
        public float MaxPitch => maxPitch;
        public float MinVolume => minVolume;
        public float MaxVolume => maxVolume;
        public float RangeUpgradeMultiplier => rangeUpgradeMultiplier;
        public float StrengthUpgradeMultiplier => strengthUpgradeMultiplier;
        public float AudioUpgradeMultiplier => audioUpgradeMultiplier;
    }
}