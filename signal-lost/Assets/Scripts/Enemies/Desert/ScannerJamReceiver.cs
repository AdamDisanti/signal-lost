using System.Collections.Generic;
using SignalLost.Scanner;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Something that jams the player's scanner while the player is within its radius (a Dish Meerkat's radar dish
    /// while it is popped up). Jammers register themselves; <see cref="ScannerJamReceiver"/> does the jamming.
    /// </summary>
    public interface IScannerJammer
    {
        Vector3 JamPosition { get; }
        float JamRadius { get; }
    }

    /// <summary>
    /// Sits on the player's ScannerRig (next to Amanda's ScannerController) and jams the scanner while any
    /// <see cref="IScannerJammer"/> is in range: the direction arrow disappears, the strength meter flickers with
    /// noise, the beeps stop and a static crackle plays instead.
    ///
    /// It does this from the outside, without changing the scanner's own scripts: it switches ScannerController off
    /// while jammed (which clears its target, so the arrow hides and beeps stop) and back on afterwards. It is added
    /// to the scene's scanner automatically the first time a jammer appears, so no prefab or scene setup is needed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScannerJamReceiver : MonoBehaviour
    {
        private static readonly List<IScannerJammer> Jammers = new List<IScannerJammer>();
        private static ScannerJamReceiver instance;

        private ScannerController controller;
        private ScannerToggle toggle;
        private Slider meter;
        private AudioSource staticAudio;
        private AudioClip staticClip;
        private bool jamming, disabledController;

        /// <summary>True while the scanner is raised and being jammed (for the on-screen readout).</summary>
        public static bool IsJammedAndRaised => instance != null && instance.jamming && instance.toggle != null && instance.toggle.IsRaised;

        public static void Register(IScannerJammer jammer)
        {
            if (jammer == null || Jammers.Contains(jammer)) return;
            Jammers.Add(jammer);
            EnsureReceiver();
        }

        public static void Unregister(IScannerJammer jammer) => Jammers.Remove(jammer);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Jammers.Clear();
            instance = null;
        }

        private static void EnsureReceiver()
        {
            if (instance != null) return;
            var scanner = FindAnyObjectByType<ScannerController>(FindObjectsInactive.Include);
            if (scanner == null) return; // a scene without a scanner: nothing to jam
            instance = scanner.GetComponent<ScannerJamReceiver>();
            if (instance == null) instance = scanner.gameObject.AddComponent<ScannerJamReceiver>();
            FodderStatusOverlay.Ensure();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
            controller = GetComponent<ScannerController>();
            toggle = GetComponent<ScannerToggle>();
            if (toggle == null) toggle = GetComponentInChildren<ScannerToggle>(true);
            meter = GetComponentInChildren<Slider>(true);

            staticAudio = gameObject.AddComponent<AudioSource>();
            staticAudio.playOnAwake = false;
            staticAudio.loop = true;
            staticAudio.spatialBlend = 0f;
            staticAudio.volume = 0.25f;
            staticClip = CreateStaticClip();
            staticAudio.clip = staticClip;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (staticClip != null) Destroy(staticClip);
        }

        private void OnDisable() => SetJammed(false);

        // After the scanner's own Update/HUD, so the noisy meter value wins this frame.
        private void LateUpdate()
        {
            bool jammed = false;
            Vector3 here = transform.position;
            for (int i = Jammers.Count - 1; i >= 0; i--)
            {
                IScannerJammer jammer = Jammers[i];
                if (jammer == null || (jammer is Object unityObject && unityObject == null))
                {
                    Jammers.RemoveAt(i);
                    continue;
                }
                Vector3 offset = jammer.JamPosition - here;
                offset.y = 0f;
                if (offset.sqrMagnitude <= jammer.JamRadius * jammer.JamRadius) jammed = true;
            }
            SetJammed(jammed);

            bool raised = toggle != null && toggle.IsRaised;
            if (jamming && raised && meter != null)
                meter.value = Mathf.PerlinNoise(Time.time * 9f, 0.3f) * (Random.value < 0.2f ? 1f : 0.5f);

            if (jamming && raised)
            {
                if (!staticAudio.isPlaying) staticAudio.Play();
                staticAudio.pitch = 0.8f + Random.value * 0.4f;
            }
            else if (staticAudio.isPlaying) staticAudio.Stop();
        }

        private void SetJammed(bool on)
        {
            if (on == jamming) return;
            jamming = on;
            if (controller == null) return;
            if (on && controller.enabled)
            {
                controller.enabled = false; // clears its target: no arrow, no beeps
                disabledController = true;
            }
            else if (!on && disabledController)
            {
                controller.enabled = true;
                disabledController = false;
            }
        }

        private static AudioClip CreateStaticClip()
        {
            const int sampleRate = 22050;
            const float seconds = 0.5f;
            int count = Mathf.RoundToInt(sampleRate * seconds);
            var samples = new float[count];
            var random = new System.Random(7);
            float crackle = 0f;
            for (int i = 0; i < count; i++)
            {
                // Hiss plus random pops, so it reads as radio static.
                if (random.NextDouble() < 0.002) crackle = 1f;
                crackle *= 0.995f;
                samples[i] = ((float)random.NextDouble() * 2f - 1f) * (0.25f + 0.6f * crackle);
            }
            AudioClip clip = AudioClip.Create("ScannerStatic", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
