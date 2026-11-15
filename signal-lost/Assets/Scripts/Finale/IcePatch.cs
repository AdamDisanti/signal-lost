using System.Collections.Generic;
using SignalLost.Enemies.Desert;
using SignalLost.Mudlands;
using UnityEngine;

namespace SignalLost.Finale
{
    /// <summary>
    /// A sheet of ice left by the Cooling System's freezing sweep. Standing on it slows the player (through
    /// <see cref="PlayerSlowReceiver.ExtraSlow"/>, like Sludge's oil). It glints, then melts away over its last second.
    /// </summary>
    public sealed class IcePatch : MonoBehaviour
    {
        private static readonly List<IcePatch> Live = new List<IcePatch>();
        private static bool hooked;

        private float radius, slow, life, age;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Live.Clear();
            hooked = false;
        }

        public static IcePatch Spawn(Vector3 ground, float radius, float life, float slow)
        {
            var go = new GameObject("Ice Patch");
            go.transform.position = ground + Vector3.up * 0.05f;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var patch = go.AddComponent<IcePatch>();
            patch.radius = radius;
            patch.life = life;
            patch.slow = slow;
            Transform sheet = MudArt.Part(go.transform, "Sheet", Vector3.zero, Quaternion.identity, "finale_ice", Sheet, false);
            sheet.GetComponent<MeshRenderer>().sharedMaterial = MudArt.Clear(FinalePalette.Ice, 0.6f);
            MudArt.Part(go.transform, "Shards", Vector3.zero, Quaternion.identity, "finale_iceshards", Shards);
            go.transform.localScale = new Vector3(radius, 1f, radius);
            if (!hooked)
            {
                PlayerSlowReceiver.ExtraSlow += SlowAt;
                hooked = true;
            }
            PlayerSlowReceiver.Ensure();
            return patch;
        }

        /// <summary>The strongest slow of any ice under these feet (0 to 1).</summary>
        public static float SlowAt(Vector3 feet)
        {
            float strongest = 0f;
            foreach (IcePatch patch in Live)
            {
                if (patch == null) continue;
                Vector3 offset = feet - patch.transform.position;
                if (Mathf.Abs(offset.y) > 1.5f) continue;
                offset.y = 0f;
                if (offset.sqrMagnitude <= patch.radius * patch.radius) strongest = Mathf.Max(strongest, patch.slow);
            }
            return strongest;
        }

        /// <summary>Removes every patch (a phase retry).</summary>
        public static void ClearAll()
        {
            foreach (IcePatch patch in Live.ToArray())
                if (patch != null) Destroy(patch.gameObject);
            Live.Clear();
        }

        private void OnEnable() => Live.Add(this);
        private void OnDisable() => Live.Remove(this);

        private void Update()
        {
            age += Time.deltaTime;
            float melt = Mathf.Clamp01((life - age) / 1f);
            transform.localScale = new Vector3(radius, Mathf.Lerp(0.2f, 1f, melt), radius) * Mathf.Lerp(0.9f, 1f, melt);
            if (age >= life) Destroy(gameObject);
        }

        // A flat, faceted disc of ice (unit radius).
        private static void Sheet(MeshKit k)
        {
            const int sides = 9;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                float r0 = 0.85f + 0.15f * Mathf.Sin(i * 2.3f), r1 = 0.85f + 0.15f * Mathf.Sin((i + 1) * 2.3f);
                k.Tri(new Vector3(0f, 0.04f, 0f), new Vector3(Mathf.Sin(a0) * r0, 0f, Mathf.Cos(a0) * r0), new Vector3(Mathf.Sin(a1) * r1, 0f, Mathf.Cos(a1) * r1), FinalePalette.Ice);
            }
        }

        // A few icicle shards poking up at the edge.
        private static void Shards(MeshKit k)
        {
            var random = new System.Random(5);
            for (int i = 0; i < 7; i++)
            {
                float a = (float)random.NextDouble() * Mathf.PI * 2f, r = 0.45f + (float)random.NextDouble() * 0.45f;
                Vector3 at = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                Quaternion lean = Quaternion.Euler((float)random.NextDouble() * 30f - 15f, 0f, (float)random.NextDouble() * 30f - 15f);
                k.Cone(at, lean, 0.09f + (float)random.NextDouble() * 0.06f, 0.35f + (float)random.NextDouble() * 0.5f, 5, new Color(0.8f, 0.95f, 1f), 0.8f);
            }
        }
    }
}
