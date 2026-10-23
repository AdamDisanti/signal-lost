using System.Collections.Generic;
using SignalLost.Core;
using UnityEngine;

namespace SignalLost.Audio
{
    /// <summary>Which volume slider a sound follows (Settings > Sound).</summary>
    public enum SoundBus
    {
        /// <summary>Gameplay sounds: "Sound effects" slider.</summary>
        Effects,
        /// <summary>Background loops: the "Music" slider until the game has music.</summary>
        Ambience,
        /// <summary>Menu sounds: "Sound effects" slider, and they keep playing while the pause menu has the audio paused.</summary>
        Interface,
    }

    /// <summary>
    /// Loads and plays the game's sound effects. Every clip lives in <c>Assets/Audio/Resources/SignalLostAudio/</c>
    /// and is played by file name, so swapping a sound only means replacing its .wav with one of the same name.
    /// One-shots use a small pool of AudioSources under the <see cref="SoundDirector"/>.
    /// </summary>
    public static class Sfx
    {
        public const string Folder = "SignalLostAudio/";
        private const int MaxVoices = 48;

        private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        private static readonly List<AudioSource> pool = new List<AudioSource>();
        private static Transform root;

        internal static void SetRoot(Transform parent) => root = parent;

        /// <summary>
        /// Volume over distance for world loops (0 = at the source, 1 = maxDistance): full volume very close,
        /// half volume by 15% of the range, a quiet background from 40%, silent at the edge.
        /// </summary>
        public static readonly AnimationCurve NearbyFalloff = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.05f, 0.9f), new Keyframe(0.15f, 0.5f),
            new Keyframe(0.4f, 0.15f), new Keyframe(0.75f, 0.04f), new Keyframe(1f, 0f));

        public static float BusVolume(SoundBus bus) =>
            bus == SoundBus.Ambience ? GameSettings.MusicVolume : GameSettings.EffectsVolume;

        /// <summary>The clip with this file name (no extension), or null with a warning if it is missing.</summary>
        public static AudioClip Clip(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (!cache.TryGetValue(name, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>(Folder + name);
                if (clip == null) Debug.LogWarning($"[Sfx] Missing sound '{name}' in Resources/{Folder}");
                cache[name] = clip;
            }
            return clip;
        }

        /// <summary>A random variant: Pick("crab_hit", 2) gives crab_hit_1 or crab_hit_2.</summary>
        public static string Pick(string prefix, int count) => prefix + "_" + Random.Range(1, count + 1);

        /// <summary>Plays a sound that isn't anywhere in the world (heard the same from every direction).</summary>
        public static AudioSource Play(string name, float volume = 1f, float pitch = 1f, SoundBus bus = SoundBus.Effects) =>
            Start(name, null, volume, pitch, bus, 0f);

        /// <summary>Plays a sound at a point in the world; it gets quieter with distance and is silent past maxDistance.</summary>
        public static AudioSource PlayAt(string name, Vector3 position, float volume = 1f, float pitch = 1f, float maxDistance = 30f) =>
            Start(name, position, volume, pitch, SoundBus.Effects, maxDistance);

        private static AudioSource Start(string name, Vector3? position, float volume, float pitch, SoundBus bus, float maxDistance)
        {
            AudioClip clip = Clip(name);
            AudioSource source = clip != null ? Rent() : null;
            if (source == null) return null;

            source.clip = clip;
            source.loop = false;
            source.pitch = pitch;
            source.volume = Mathf.Clamp01(volume * BusVolume(bus));
            source.ignoreListenerPause = bus == SoundBus.Interface;
            source.dopplerLevel = 0f;
            if (position.HasValue)
            {
                source.transform.position = position.Value;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = Mathf.Min(2f, maxDistance * 0.5f);
                source.maxDistance = maxDistance;
            }
            else
            {
                source.transform.localPosition = Vector3.zero;
                source.spatialBlend = 0f;
            }
            source.Play();
            return source;
        }

        private static AudioSource Rent()
        {
            if (root == null) return null;
            AudioSource oldest = null;
            foreach (AudioSource source in pool)
            {
                if (source == null) continue;
                if (!source.isPlaying) return source;
                if (oldest == null || source.time / Mathf.Max(0.01f, source.clip.length) > oldest.time / Mathf.Max(0.01f, oldest.clip.length))
                    oldest = source;
            }
            if (pool.Count < MaxVoices)
            {
                var go = new GameObject("Sfx Voice");
                go.transform.SetParent(root, false);
                var created = go.AddComponent<AudioSource>();
                created.playOnAwake = false;
                pool.Add(created);
                return created;
            }
            if (oldest != null) oldest.Stop(); // all busy: cut the one closest to finishing
            return oldest;
        }

        /// <summary>
        /// Adds a looping sound to <paramref name="host"/> (on a child object). Its loudness follows the bus slider and
        /// <see cref="SfxLoop.Level"/>. Spatial loops fade out with distance between minDistance and maxDistance.
        /// </summary>
        public static SfxLoop AddLoop(GameObject host, string name, float volume, bool spatial, float minDistance = 2f,
            float maxDistance = 25f, SoundBus bus = SoundBus.Effects, string childName = "Sound Loop")
        {
            AudioClip clip = Clip(name);
            if (clip == null || host == null) return null;
            var go = new GameObject(childName);
            go.transform.SetParent(host.transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            source.spatialBlend = spatial ? 1f : 0f;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            if (spatial)
            {
                // Loud up close, drops off quickly, and is fully silent at maxDistance.
                source.rolloffMode = AudioRolloffMode.Custom;
                source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, NearbyFalloff);
            }
            else source.rolloffMode = AudioRolloffMode.Linear;
            source.time = Random.Range(0f, clip.length * 0.9f); // so neighbouring loops don't play in step
            var loop = go.AddComponent<SfxLoop>();
            loop.Init(source, volume, bus);
            return loop;
        }
    }
}
