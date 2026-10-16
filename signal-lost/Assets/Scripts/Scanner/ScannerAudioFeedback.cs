using UnityEngine;

namespace SignalLost.Scanner
{
    [RequireComponent(typeof(AudioSource))]

    public sealed class ScannerAudioFeedback : MonoBehaviour
    {
        [SerializeField] private ScannerController scanner;
        [SerializeField] private ScannerToggle scannerToggle;
        [SerializeField] private ScannerConfig config;

        private AudioSource audioSource;
        private AudioClip beepClip;
        private float nextBeepTime;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            
            if (scanner == null || scannerToggle == null || config == null)
            {
                Debug.LogError("ScannerAudioFeedback requires scanner, toggle, and config references.", this);
                enabled = false;
                return;
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;

            beepClip = CreateBeepClip();
            audioSource.clip = beepClip;
        }

        private void Update()
        {
            if (!scannerToggle.IsRaised || scanner.CurrentTarget == null)
            {
                audioSource.Stop();
                nextBeepTime = 0f;
                return;
            }

            if (Time.time < nextBeepTime)
            {
                return;
            }

            float strength = scanner.SignalStrength01;
            float upgrade = Mathf.Max(0.1f, config.AudioUpgradeMultiplier);

            float interval = Mathf.Lerp(config.FarBeepInterval, config.CloseBeepInterval, strength) / upgrade;

            audioSource.pitch = Mathf.Lerp(config.MinPitch, config.MaxPitch, strength) * upgrade;
            audioSource.volume = Mathf.Clamp01(Mathf.Lerp(config.MinVolume, config.MaxVolume, strength) * upgrade);
            audioSource.Play();

            nextBeepTime = Time.time + Mathf.Max(0.05f, interval);
        }

        private void OnDisable()
        {
            if (audioSource != null)
            {
                audioSource.Stop();
            }
        }

        private void OnDestroy()
        {
            if (beepClip != null)
            {
                Destroy(beepClip);
            }
        }

        private static AudioClip CreateBeepClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.08f;
            const float frequency = 800f;

            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)i / sampleRate;
                float attack = Mathf.Clamp01(time / 0.005f);
                float release = Mathf.Clamp01((duration - time) / 0.02f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * attack * release * 0.35f;
            }

            AudioClip clip = AudioClip.Create("ScannerBeep", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}