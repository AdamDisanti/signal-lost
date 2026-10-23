using UnityEngine;

namespace SignalLost.Audio
{
    /// <summary>A looping AudioSource whose volume is base volume × <see cref="Level"/> × the bus slider.</summary>
    public sealed class SfxLoop : MonoBehaviour
    {
        private AudioSource source;
        private float baseVolume;
        private SoundBus bus;

        /// <summary>0 = silent, 1 = full. Change it freely; it's applied every frame.</summary>
        public float Level { get; set; } = 1f;
        public AudioSource Source => source;

        internal void Init(AudioSource audioSource, float volume, SoundBus soundBus)
        {
            source = audioSource;
            baseVolume = volume;
            bus = soundBus;
            Apply();
            source.Play();
        }

        public void SetClip(string name)
        {
            AudioClip clip = Sfx.Clip(name);
            if (clip == null || source.clip == clip) return;
            source.clip = clip;
            source.Play();
        }

        private void OnEnable()
        {
            if (source != null && source.loop && !source.isPlaying) source.Play(); // one-shot uses (e.g. the charge rise) stay stopped
        }

        private void Update() => Apply();

        private void Apply()
        {
            if (source == null) return;
            source.volume = Mathf.Clamp01(baseVolume * Level * Sfx.BusVolume(bus));
        }
    }
}
