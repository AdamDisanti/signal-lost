using System;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// The Spirit Road (quest "Three Gongs", docs/mudlands.md): a causeway of old stone slabs sunk in Sinkmire Bog. Each
    /// spirit gong rung raises one of its three sections out of the water, with bubbles and a rumble; the third makes
    /// it whole, a dry road straight across the bog from the camp side to the dam, wide enough for the car. Its slabs
    /// have glowing edges so it reads at night and in the car. Risen slabs count as dry land (<see cref="MudZone.Dry"/>),
    /// so you don't wade on them and the car doesn't stall.
    /// </summary>
    public sealed class SpiritRoad : MonoBehaviour
    {
        [Tooltip("The three sections, in the order they rise. Each holds slabs: boxes scaled to size, with a BoxCollider.")]
        [SerializeField] private Transform[] sections = Array.Empty<Transform>();
        [SerializeField, Min(0.5f)] private float sunk = 2.6f;
        [SerializeField, Min(0.5f)] private float riseSeconds = 3.2f;

        private Vector3[] rest;
        private float[] startedAt;
        private bool[] dry;

        public int SectionCount => sections.Length;
        public int Risen { get; private set; }
        public event Action<int> SectionRisen;

        public void Configure(Transform[] parts) => sections = parts;

        private static readonly Color Stone = new Color(0.36f, 0.35f, 0.4f), StoneEdge = new Color(0.27f, 0.27f, 0.32f);
        public static readonly Color Spirit = new Color(0.35f, 0.95f, 0.9f);

        private void Awake()
        {
            rest = new Vector3[sections.Length];
            startedAt = new float[sections.Length];
            dry = new bool[sections.Length];
            for (int i = 0; i < sections.Length; i++)
            {
                startedAt[i] = -1f;
                if (sections[i] == null) continue;
                rest[i] = sections[i].localPosition;
                sections[i].localPosition = rest[i] + Vector3.down * sunk;
                foreach (BoxCollider slab in sections[i].GetComponentsInChildren<BoxCollider>()) Dress(slab.transform);
            }
        }

        // The look of one slab (its transform is scaled to the slab's size; the mesh is a unit box).
        private static void Dress(Transform slab)
        {
            if (slab.GetComponent<MeshFilter>() != null) return;
            (Mesh mesh, Material[] materials) = MudArt.Cached("spirit_slab", k =>
            {
                k.Box(Vector3.zero, Vector3.one, Stone);
                k.Box(new Vector3(0f, -0.35f, 0f), new Vector3(1.04f, 0.3f, 1.02f), StoneEdge); // a weathered lip at the waterline
                foreach (float x in new[] { -0.42f, 0.42f })
                    k.Box(new Vector3(x, 0.5f, 0f), new Vector3(0.035f, 0.04f, 0.96f), Spirit, 3f); // glowing edge lines
                k.Box(new Vector3(0f, 0.5f, 0.3f), new Vector3(0.12f, 0.03f, 0.08f), Spirit, 2.5f);  // rune marks down the middle
                k.Box(new Vector3(0f, 0.5f, -0.3f), new Vector3(0.12f, 0.03f, 0.08f), Spirit, 2.5f);
            });
            slab.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            slab.gameObject.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }

        /// <summary>Raises the next section (the next gong rang). <paramref name="instant"/>: no animation (catching up).</summary>
        public void RaiseNext(bool instant = false)
        {
            if (Risen >= sections.Length) return;
            int index = Risen++;
            if (sections[index] == null) return;
            if (instant)
            {
                sections[index].localPosition = rest[index];
                MakeDry(index);
                return;
            }
            startedAt[index] = Time.time;
            Vector3 middle = Middle(index);
            Sfx.PlayAt("stone_rise", middle, 1f, 0.7f, 120f);
            Sfx.PlayAt("water_lap", middle, 1f, 0.8f, 90f);
            CameraShake.Add(0.25f);
        }

        private void Update()
        {
            for (int i = 0; i < sections.Length; i++)
            {
                if (startedAt[i] < 0f || dry[i] || sections[i] == null) continue;
                float k = Mathf.Clamp01((Time.time - startedAt[i]) / riseSeconds);
                float eased = 1f - (1f - k) * (1f - k) * (1f - k);
                sections[i].localPosition = rest[i] + Vector3.down * (sunk * (1f - eased)) +
                                            (k < 1f ? new Vector3(0f, Mathf.Sin(Time.time * 40f) * 0.02f * (1f - k), 0f) : Vector3.zero);
                // Bubbles and spray along the slabs as they break the surface.
                if (k < 0.85f && Random.value < 0.5f)
                {
                    var slabs = sections[i].GetComponentsInChildren<BoxCollider>();
                    if (slabs.Length > 0)
                    {
                        Transform slab = slabs[Random.Range(0, slabs.Length)].transform;
                        Vector3 at = slab.position + slab.right * Random.Range(-2f, 2f) + slab.forward * Random.Range(-2f, 2f);
                        at.y = MudlandsWater.Level;
                        ParticleBurst.Spawn(ParticleBurst.Kind.Water, at, Vector3.up, 6, 0.35f, 2.5f, 60f, new Color(0.45f, 0.5f, 0.45f, 0.7f));
                    }
                }
                if (k >= 1f)
                {
                    sections[i].localPosition = rest[i];
                    MakeDry(i);
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, Middle(i) + Vector3.up * 0.6f, Vector3.up, 30, 0.08f, 3f, 70f, Spirit);
                }
            }
        }

        private void MakeDry(int index)
        {
            if (dry[index]) return;
            dry[index] = true;
            Physics.SyncTransforms();
            foreach (BoxCollider slab in sections[index].GetComponentsInChildren<BoxCollider>()) MudZone.Dry.Add(slab.bounds);
            SectionRisen?.Invoke(index);
        }

        private Vector3 Middle(int index)
        {
            var slabs = sections[index].GetComponentsInChildren<BoxCollider>();
            if (slabs.Length == 0) return sections[index].position;
            Vector3 sum = Vector3.zero;
            foreach (BoxCollider slab in slabs) sum += slab.transform.position;
            return sum / slabs.Length + Vector3.up * sunk;
        }
    }

    /// <summary>The Mudlands' still water level (the bog, the creek), for effects that sit on the surface.</summary>
    public static class MudlandsWater
    {
        public const float Level = -0.55f;
    }
}
