using UnityEngine;

namespace SignalLost.Effects
{
    /// <summary>
    /// Makes a light flicker like a fire (smooth noise) or an electrical fault (random sharp pops).
    /// Put it on the Light of a campfire or a sparking wreck.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class FlickerLight : MonoBehaviour
    {
        [SerializeField] private bool electrical;
        [SerializeField, Range(0f, 1f)] private float strength = 0.35f;
        [SerializeField, Min(0.1f)] private float speed = 7f;

        private Light glow;
        private float baseIntensity, seed, popUntil;

        private void Awake()
        {
            glow = GetComponent<Light>();
            baseIntensity = glow.intensity;
            seed = Random.value * 100f;
        }

        private void Update()
        {
            float noise = Mathf.PerlinNoise(seed, Time.time * speed);
            if (!electrical)
            {
                glow.intensity = baseIntensity * (1f - strength + strength * 2f * noise);
                return;
            }
            // Electrical: mostly dim with bright random pops.
            if (Time.time >= popUntil && Random.value < Time.deltaTime * 3f) popUntil = Time.time + Random.Range(0.04f, 0.14f);
            glow.intensity = baseIntensity * (Time.time < popUntil ? 1.4f : 0.25f + 0.3f * noise);
        }
    }
}
