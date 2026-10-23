using UnityEngine;

namespace SignalLost.Effects
{
    /// <summary>
    /// Screen shake for big impacts: call <see cref="Add"/> with some trauma (0 to 1). It shakes whichever camera is
    /// the main camera, nudging it a little off its normal spot and settling back, so it works with the player's camera
    /// and with cutscene cameras alike. Trauma decays on its own.
    /// </summary>
    public sealed class CameraShake : MonoBehaviour
    {
        private static CameraShake instance;
        private float trauma;
        private Transform shaken;
        private Vector3 applied;
        private Quaternion appliedRotation = Quaternion.identity;

        /// <param name="amount">0 to 1; stacks up to 1. About 0.2 is a thud, 0.6 an eruption, 1 an explosion.</param>
        public static void Add(float amount)
        {
            if (instance == null)
            {
                var go = new GameObject("Camera Shake");
                instance = go.AddComponent<CameraShake>();
            }
            instance.trauma = Mathf.Clamp01(instance.trauma + amount);
        }

        /// <summary>Shake strength right now (0 to 1).</summary>
        public static float Trauma => instance != null ? instance.trauma : 0f;

        private void LateUpdate()
        {
            Remove();
            trauma = Mathf.Max(0f, trauma - Time.deltaTime * 0.9f);
            if (trauma <= 0f) return;
            Camera camera = Camera.main;
            if (camera == null) return;
            shaken = camera.transform;
            float k = trauma * trauma;
            float t = Time.time * 25f;
            applied = new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, Mathf.PerlinNoise(0.3f, t) - 0.5f, 0f) * 0.5f * k;
            appliedRotation = Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(t, 7.7f) - 0.5f) * 6f * k);
            shaken.localPosition += applied;
            shaken.localRotation *= appliedRotation;
        }

        private void Remove()
        {
            if (shaken == null) return;
            shaken.localPosition -= applied;
            shaken.localRotation *= Quaternion.Inverse(appliedRotation);
            applied = Vector3.zero;
            appliedRotation = Quaternion.identity;
            shaken = null;
        }

        private void OnDisable() => Remove();
    }
}
