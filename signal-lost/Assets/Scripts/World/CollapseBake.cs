using System;
using UnityEngine;

namespace SignalLost.World
{
    /// <summary>
    /// A rockfall recorded from a real physics simulation (11-09), so it plays out exactly the same every time while
    /// every bounce and knock between the rocks is the physics engine's own. The level builder runs the simulation
    /// once (rigid bodies falling onto the debris cone and each other until they all come to rest), checks the result
    /// leaves a way through, and stores each rock's path here; <see cref="WormLair"/> plays it back.
    ///
    /// Each rock has a track: it starts when the rock comes loose and has a key (position and rotation) every
    /// <see cref="FrameTime"/> until the rock last moves. Keys are packed into 14 bytes: the position as three 16-bit
    /// steps across <see cref="boundsMin"/>..+<see cref="boundsSize"/> (about 2 mm), the rotation as four 16-bit numbers.
    /// Positions are relative to the lair. The hard knocks (for dust and screen shake) are stored as a timed list.
    /// </summary>
    public sealed class CollapseBake : ScriptableObject
    {
        public const int KeyBytes = 14;

        [SerializeField] private float frameTime = 1f / 30f;
        [SerializeField] private float duration;
        [Tooltip("When the last rock stops moving fast enough to hurt.")]
        [SerializeField] private float dangerTime;
        [SerializeField] private Vector3 boundsMin, boundsSize = Vector3.one;
        [SerializeField] private float[] startTimes = Array.Empty<float>();
        [SerializeField] private int[] firstKeys = Array.Empty<int>();
        [SerializeField] private int[] keyCounts = Array.Empty<int>();
        [SerializeField] private byte[] keys = Array.Empty<byte>();
        [SerializeField] private float[] impactTimes = Array.Empty<float>();
        [SerializeField] private Vector3[] impactPoints = Array.Empty<Vector3>();
        [SerializeField] private float[] impactSizes = Array.Empty<float>();

        public float FrameTime => frameTime;
        /// <summary>Seconds from the first rock coming loose to the last one coming to rest.</summary>
        public float Duration => duration;
        public float DangerTime => dangerTime;
        public int RockCount => startTimes.Length;
        public int ImpactCount => impactTimes.Length;
        public int KeyCount => keys.Length / KeyBytes;

        public float StartTime(int rock) => startTimes[rock];
        /// <summary>When this rock stops for good.</summary>
        public float EndTime(int rock) => startTimes[rock] + Mathf.Max(0, keyCounts[rock] - 1) * frameTime;

        public float ImpactTime(int index) => impactTimes[index];
        public Vector3 ImpactPoint(int index) => impactPoints[index];
        public float ImpactSize(int index) => impactSizes[index];

        /// <summary>Where a rock is at a time (relative to the lair): before its track, its first key; after, its last.</summary>
        public void Sample(int rock, float time, out Vector3 position, out Quaternion rotation)
        {
            int count = keyCounts[rock], first = firstKeys[rock];
            float k = (time - startTimes[rock]) / frameTime;
            if (k <= 0f || count == 1)
            {
                Read(first, out position, out rotation);
                return;
            }
            if (k >= count - 1)
            {
                Read(first + count - 1, out position, out rotation);
                return;
            }
            int index = (int)k;
            Read(first + index, out Vector3 p0, out Quaternion r0);
            Read(first + index + 1, out Vector3 p1, out Quaternion r1);
            float t = k - index;
            position = Vector3.LerpUnclamped(p0, p1, t);
            rotation = Quaternion.SlerpUnclamped(r0, r1, t);
        }

        private void Read(int key, out Vector3 position, out Quaternion rotation)
        {
            int o = key * KeyBytes;
            position = new Vector3(
                boundsMin.x + boundsSize.x * (U16(o) / 65535f),
                boundsMin.y + boundsSize.y * (U16(o + 2) / 65535f),
                boundsMin.z + boundsSize.z * (U16(o + 4) / 65535f));
            rotation = new Quaternion(S16(o + 6) / 32767f, S16(o + 8) / 32767f, S16(o + 10) / 32767f, S16(o + 12) / 32767f);
        }

        private int U16(int offset) => keys[offset] | (keys[offset + 1] << 8);
        private int S16(int offset) => (short)(keys[offset] | (keys[offset + 1] << 8));

        /// <summary>
        /// Stores a recording (the level builder). <paramref name="positions"/> and <paramref name="rotations"/> hold
        /// every rock's keys one after another; <paramref name="counts"/> says how many each rock has.
        /// </summary>
        public void Set(float frame, float[] starts, int[] counts, Vector3[] positions, Quaternion[] rotations, float danger,
            float[] knockTimes, Vector3[] knockPoints, float[] knockSizes)
        {
            frameTime = frame;
            startTimes = starts;
            keyCounts = counts;
            firstKeys = new int[counts.Length];
            duration = 0f;
            int total = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                firstKeys[i] = total;
                total += counts[i];
                duration = Mathf.Max(duration, starts[i] + Mathf.Max(0, counts[i] - 1) * frame);
            }
            dangerTime = Mathf.Min(danger, duration);

            var bounds = new Bounds(positions.Length > 0 ? positions[0] : Vector3.zero, Vector3.zero);
            foreach (Vector3 p in positions) bounds.Encapsulate(p);
            boundsMin = bounds.min;
            boundsSize = Vector3.Max(bounds.size, Vector3.one * 0.01f);

            keys = new byte[total * KeyBytes];
            Quaternion previous = Quaternion.identity;
            for (int i = 0; i < total; i++)
            {
                int o = i * KeyBytes;
                Vector3 p = positions[i];
                Put(o, Mathf.RoundToInt(Mathf.Clamp01((p.x - boundsMin.x) / boundsSize.x) * 65535f));
                Put(o + 2, Mathf.RoundToInt(Mathf.Clamp01((p.y - boundsMin.y) / boundsSize.y) * 65535f));
                Put(o + 4, Mathf.RoundToInt(Mathf.Clamp01((p.z - boundsMin.z) / boundsSize.z) * 65535f));
                // Neighbouring keys keep the same sign, so blending between them takes the short way round.
                Quaternion r = rotations[i].normalized;
                if (i > 0 && Quaternion.Dot(previous, r) < 0f) r = new Quaternion(-r.x, -r.y, -r.z, -r.w);
                previous = r;
                Put(o + 6, Mathf.RoundToInt(r.x * 32767f));
                Put(o + 8, Mathf.RoundToInt(r.y * 32767f));
                Put(o + 10, Mathf.RoundToInt(r.z * 32767f));
                Put(o + 12, Mathf.RoundToInt(r.w * 32767f));
            }
            impactTimes = knockTimes;
            impactPoints = knockPoints;
            impactSizes = knockSizes;
        }

        private void Put(int offset, int value)
        {
            keys[offset] = (byte)(value & 0xff);
            keys[offset + 1] = (byte)((value >> 8) & 0xff);
        }
    }
}
