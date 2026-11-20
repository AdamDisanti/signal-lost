using UnityEngine;

namespace SignalLost.Audio
{
    /// <summary>
    /// A looping AudioSource whose volume is base volume × <see cref="Level"/> × the bus slider. While it is silent
    /// (volume 0) or the listener is beyond its maxDistance it pauses, so it doesn't hold one of Unity's voices, and it
    /// carries on where it was when it can be heard again (11-20: 41 torch fires across the Mudlands were taking every
    /// voice, so the player's own jump and laser sounds were dropped or cut off).
    /// </summary>
    public sealed class SfxLoop : MonoBehaviour
    {
        private AudioSource source;
        private float baseVolume;
        private SoundBus bus;
        private bool pausedHere;

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
            pausedHere = false;
            source.Play();
        }

        private void OnEnable()
        {
            if (source == null) return;
            if (pausedHere) { pausedHere = false; source.UnPause(); }
            else if (source.loop && !source.isPlaying) source.Play(); // one-shot uses (e.g. the charge rise) stay stopped
        }

        private void Update() => Apply();

        private void Apply()
        {
            if (source == null) return;
            float volume = Mathf.Clamp01(baseVolume * Level * Sfx.BusVolume(bus));
            source.volume = volume;

            bool silent = volume <= 0.0001f;
            if (!silent && source.spatialBlend > 0.5f && Sfx.ListenerPosition(out Vector3 ears))
            {
                float range = source.maxDistance * (pausedHere ? 1f : 1.05f); // a little slack so it doesn't flicker at the edge
                silent = (source.transform.position - ears).sqrMagnitude > range * range;
            }
            if (silent && source.isPlaying)
            {
                source.Pause();
                pausedHere = true;
            }
            else if (!silent && pausedHere)
            {
                pausedHere = false;
                source.UnPause();
            }
        }
    }
}
