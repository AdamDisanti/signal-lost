using UnityEngine;

namespace SignalLost.Player
{
    /// <summary>
    /// Makes a pickup lying in the wreckage easy to spot: its light pulses and, optionally, the model bobs and turns.
    /// Purely visual; collecting is handled by <see cref="TutorialPickup"/>.
    /// </summary>
    public sealed class PickupGlint : MonoBehaviour
    {
        [SerializeField] private Light glow;
        [SerializeField] private Transform model;
        [SerializeField, Min(0f)] private float pulseSpeed = 2.5f;
        [SerializeField, Min(0f)] private float bobHeight = 0.06f;
        [SerializeField] private float turnSpeed = 35f;
        private float baseIntensity;
        private Vector3 modelRest;

        private void Awake()
        {
            if (glow != null) baseIntensity = glow.intensity;
            if (model != null) modelRest = model.localPosition;
        }

        private void Update()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
            if (glow != null) glow.intensity = baseIntensity * Mathf.Lerp(0.35f, 1f, pulse);
            if (model == null) return;
            model.localPosition = modelRest + Vector3.up * (bobHeight * pulse);
            model.Rotate(0f, turnSpeed * Time.deltaTime, 0f, Space.World);
        }
    }
}
