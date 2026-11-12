using System.Collections;
using SignalLost.Combat;
using SignalLost.Effects;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Melee
{
    /// <summary>
    /// The knuckles' effects, in the mining laser's gauss style (its cyan energy, crackling jagged arcs, white-hot
    /// flashes): lightning bolts between two points that flicker and fade, shock rings, glow pops and spark showers.
    /// Placeholder effects built from LineRenderers, primitives and <see cref="ParticleBurst"/>, like the laser's.
    /// </summary>
    public static class GaussFx
    {
        public static readonly Color Energy = new Color(0.4f, 0.9f, 1f);
        public static readonly Color Hot = new Color(0.85f, 0.97f, 1f);

        private static Material energy, hot, soft;
        private static MonoBehaviour runner;

        /// <summary>Plain glowing cyan (the laser's energy material).</summary>
        public static Material EnergyMaterial => energy != null ? energy : energy = Unlit("Gauss Energy", Energy);
        /// <summary>White-hot, for cores and full-power moments.</summary>
        public static Material HotMaterial => hot != null ? hot : hot = Unlit("Gauss Hot", Hot);
        /// <summary>Additive soft glow, for lines that should bloom rather than read as solid.</summary>
        public static Material SoftMaterial => soft != null ? soft : soft = EffectMaterials.Additive(EffectMaterials.Look.Beam);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            energy = hot = soft = null;
            runner = null;
        }

        /// <summary>Somewhere to run effect coroutines that outlives whoever started them (one per scene).</summary>
        public static Coroutine Run(IEnumerator routine)
        {
            if (runner == null) runner = new GameObject("Gauss Effects").AddComponent<Runner>();
            return runner.StartCoroutine(routine);
        }

        private sealed class Runner : MonoBehaviour { }

        /// <summary>A jagged lightning bolt from <paramref name="from"/> to <paramref name="to"/> that crackles and fades.</summary>
        public static void Bolt(Vector3 from, Vector3 to, float width = 0.09f, float life = 0.22f, bool hot = false) =>
            Run(BoltRoutine(from, to, width, life, hot));

        private static IEnumerator BoltRoutine(Vector3 from, Vector3 to, float width, float life, bool isHot)
        {
            LineRenderer core = Line("Gauss Bolt", isHot ? HotMaterial : EnergyMaterial, width);
            LineRenderer glow = Line("Gauss Bolt Glow", SoftMaterial, width * 4f);
            glow.startColor = glow.endColor = new Color(Energy.r, Energy.g, Energy.b, 0.55f);
            int points = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(from, to) / 0.6f) + 2, 4, 18);
            core.positionCount = glow.positionCount = points;
            float age = 0f, redraw = 0f;
            while (age < life)
            {
                if (age >= redraw)
                {
                    redraw = age + 0.035f; // re-jag so it crackles
                    Jag(core, from, to, points, 0.28f);
                    for (int i = 0; i < points; i++) glow.SetPosition(i, core.GetPosition(i));
                }
                float k = 1f - age / life;
                core.widthMultiplier = width * (0.4f + 0.6f * k);
                glow.widthMultiplier = width * 4f * k;
                age += Time.deltaTime;
                yield return null;
            }
            Object.Destroy(core.gameObject);
            Object.Destroy(glow.gameObject);
        }

        /// <summary>A flat shock ring that races out to <paramref name="radius"/> (and a fainter second one behind it).</summary>
        public static void Ring(Vector3 at, float radius, float life = 0.4f, float width = 0.35f)
        {
            ExpandingRing.Spawn(EnergyMaterial, at + Vector3.up * 0.12f, radius, life, width);
            ExpandingRing.Spawn(SoftMaterial, at + Vector3.up * 0.1f, radius * 0.7f, life * 1.3f, width * 2.2f);
        }

        /// <summary>A white-hot pop and a spray of cyan sparks (and a soft glow puff).</summary>
        public static void Pop(Vector3 at, Vector3 direction, float size, int sparks = 16)
        {
            ImpactFlash.Spawn(HotMaterial, at, size * 0.6f, 0.12f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, at, direction, sparks, 0.07f, 6.5f * Mathf.Max(0.6f, size), direction == Vector3.zero ? 180f : 65f,
                Color.Lerp(Energy, Color.white, 0.35f));
            ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, at, Vector3.zero, 4, 0.45f * size, 0.6f, 90f, new Color(Energy.r, Energy.g, Energy.b, 0.55f));
        }

        // A line from a to b, kicked sideways at random in the middle (the ends stay put).
        private static void Jag(LineRenderer line, Vector3 a, Vector3 b, int points, float amount)
        {
            Vector3 along = b - a;
            Vector3 side = Vector3.Cross(along, Vector3.up);
            if (side.sqrMagnitude < 0.001f) side = Vector3.Cross(along, Vector3.right);
            side.Normalize();
            Vector3 up = Vector3.Cross(side, along).normalized;
            float reach = Mathf.Min(amount, along.magnitude * 0.12f + 0.08f);
            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1);
                float taper = Mathf.Sin(t * Mathf.PI);
                Vector3 offset = (side * Random.Range(-1f, 1f) + up * Random.Range(-1f, 1f)) * reach * taper;
                line.SetPosition(i, a + along * t + offset);
            }
        }

        private static LineRenderer Line(string name, Material material, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static Material Unlit(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = shader != null ? new Material(shader) : new Material(EffectMaterials.Additive(EffectMaterials.Look.Glow));
            material.name = name + " (runtime)";
            material.hideFlags = HideFlags.DontSave;
            material.SetColor("_BaseColor", color);
            return material;
        }
    }
}
