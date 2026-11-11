using System;
using System.Collections.Generic;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// The timing table exported with a boss's animations (tools/blender/desert_bosses.py writes
    /// <c>&lt;Boss&gt;Clips.json</c> beside the FBX). Every clip's length and its named moments ("hit", "move", "volley",
    /// ...) in seconds, so a boss's attacks land exactly when its animation does. Retime an attack in the Blender
    /// script and the fight follows. Also lists the model's materials, which the prefab builder recreates in URP.
    /// </summary>
    [Serializable]
    public sealed class BossClipTable
    {
        [Serializable]
        public sealed class Event
        {
            public string name;
            public float[] times;
        }

        [Serializable]
        public sealed class Clip
        {
            public string name;
            public float length;
            public bool loop;
            public Event[] events;
        }

        [Serializable]
        public sealed class MaterialInfo
        {
            public string name, key;
            public float[] color, emission;
            public float emissionStrength, metallic, smoothness;
        }

        public string boss;
        public Clip[] clips;
        public MaterialInfo[] materials;

        private Dictionary<string, Clip> byName;

        public static BossClipTable Parse(TextAsset asset)
        {
            if (asset == null) return new BossClipTable { clips = new Clip[0], materials = new MaterialInfo[0] };
            return JsonUtility.FromJson<BossClipTable>(asset.text);
        }

        public bool Has(string clip) => Find(clip) != null;

        /// <summary>The clip's length in seconds (1 if it is missing, with a warning).</summary>
        public float Length(string clip)
        {
            Clip c = Find(clip);
            if (c != null) return c.length;
            Debug.LogWarning($"{boss}: no clip named {clip}.");
            return 1f;
        }

        /// <summary>The time of the index-th moment called <paramref name="name"/> in a clip, or the fallback.</summary>
        public float Time(string clip, string name, int index = 0, float fallback = -1f)
        {
            Clip c = Find(clip);
            if (c?.events != null)
                foreach (Event e in c.events)
                    if (e.name == name && e.times != null && index < e.times.Length)
                        return e.times[index];
            if (fallback >= 0f) return fallback;
            Debug.LogWarning($"{boss}: clip {clip} has no moment {name}[{index}].");
            return c != null ? c.length * 0.5f : 0.5f;
        }

        /// <summary>All the times of a moment (e.g. the three volleys).</summary>
        public float[] Times(string clip, string name)
        {
            Clip c = Find(clip);
            if (c?.events != null)
                foreach (Event e in c.events)
                    if (e.name == name && e.times != null) return e.times;
            return new float[0];
        }

        private Clip Find(string clip)
        {
            if (byName == null)
            {
                byName = new Dictionary<string, Clip>();
                if (clips != null)
                    foreach (Clip c in clips)
                        if (c != null && !string.IsNullOrEmpty(c.name)) byName[c.name] = c;
            }
            byName.TryGetValue(clip, out Clip found);
            return found;
        }
    }
}
