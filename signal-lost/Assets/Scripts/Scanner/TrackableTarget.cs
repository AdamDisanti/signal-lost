using UnityEngine;

namespace SignalLost.Scanner
{
    public enum SignalType
    {
        AboveGround,
        UnderGround
    }

    public abstract class TrackableTarget : MonoBehaviour
    {
        [SerializeField] private SignalType signalType = SignalType.AboveGround;
        [SerializeField, Min(0f)] private float signalStrengthMultiplier = 1f;
        public SignalType SignalType => signalType;
        public float SignalStrengthMultiplier => signalStrengthMultiplier;
        public bool IsActive => isActiveAndEnabled;
        public bool IsDetected { get; private set; }

        public void SetDetected(bool detected) => IsDetected = detected;
        protected virtual void OnDisable() => IsDetected = false;
    }
}