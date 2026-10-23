using UnityEngine;

namespace SignalLost.Player
{
    /// <summary>
    /// Makes a pickup lying in the wreckage easy to spot: its light pulses and, optionally, the model bobs and turns.
    /// With a <see cref="visibleDistance"/>, the light only shows once the player is that close, so a hidden item
    /// still has to be searched for. Purely visual; collecting is handled by <see cref="ItemPickup"/>.
    /// </summary>
    public sealed class PickupGlint : MonoBehaviour
    {
        [SerializeField] private Light glow;
        [SerializeField] private Transform model;
        [SerializeField, Min(0f)] private float pulseSpeed = 2.5f;
        [SerializeField, Min(0f)] private float bobHeight = 0.06f;
        [SerializeField] private float turnSpeed = 35f;
        [Tooltip("The glint shows only while the player is this close. 0: always.")]
        [SerializeField, Min(0f)] private float visibleDistance;
        private float baseIntensity;
        private Camera viewer;
        private Vector3 modelRest;

        private void Awake()
        {
            if (glow != null) baseIntensity = glow.intensity;
            if (model != null) modelRest = model.localPosition;
        }

        private void Update()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
            if (glow != null)
            {
                if (viewer == null || !viewer.isActiveAndEnabled) viewer = Camera.main;
                glow.enabled = visibleDistance <= 0f || (viewer != null && Vector3.Distance(viewer.transform.position, transform.position) <= visibleDistance);
                glow.intensity = baseIntensity * Mathf.Lerp(0.35f, 1f, pulse);
            }
            if (model == null) return;
            model.localPosition = modelRest + Vector3.up * (bobHeight * pulse);
            model.Rotate(0f, turnSpeed * Time.deltaTime, 0f, Space.World);
        }
    }
}
