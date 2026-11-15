using System.Collections;
using System.Collections.Generic;
using SignalLost.Mudlands;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Finale
{
    /// <summary>
    /// Turns any creature into one of Cavendish's robot copies for the final fight (Adam, 11-09: "a robot version of
    /// them which Cavendish created"). Once the creature's own model is built it repaints it: every colour becomes
    /// metal of the same lightness (dark parts gunmetal, light parts the off-white of his mech's armour), with a little
    /// of the original hue left so each one still reads as itself, and glowing parts turn his teal. Then it bolts on robot bits:
    /// a blinking antenna, a glowing visor across the head, and rivets. The creature's own scripts and attacks are
    /// untouched; only its look changes.
    /// </summary>
    public sealed class Robotify : MonoBehaviour
    {
        [Tooltip("How much of the creature's own colours survive (0: all metal).")]
        [Range(0f, 1f)] public float keepColour = 0.28f;
        [Tooltip("The accent glow (eyes, visor, antenna light).")]
        public Color accent = FinalePalette.Teal;
        [Tooltip("Bolt on the antenna, visor and rivets.")]
        public bool addParts = true;

        private static readonly Dictionary<(Material, Color), Material> Cache = new Dictionary<(Material, Color), Material>();
        private Transform antennaLight;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Cache.Clear();

        private IEnumerator Start()
        {
            // Some creatures build parts of their models a frame or two late; repaint twice to catch them.
            Repaint();
            yield return null;
            yield return null;
            Repaint();
            if (addParts) AddParts();
        }

        private void Update()
        {
            if (antennaLight != null) antennaLight.localScale = Vector3.one * (Mathf.Repeat(Time.time, 1.2f) < 0.25f ? 1.6f : 0.8f);
        }

        /// <summary>Repaints every renderer under this object in robot metal.</summary>
        public void Repaint()
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                if (renderer.GetComponentInParent<RobotPart>() != null) continue;
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material robot = Metal(materials[i], accent, keepColour);
                    if (robot != materials[i]) { materials[i] = robot; changed = true; }
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        private static Material Metal(Material source, Color accent, float keep)
        {
            if (source == null || !source.HasProperty("_BaseColor")) return source;
            if (source.name.StartsWith("Robot ")) return source;
            if (Cache.TryGetValue((source, accent), out Material found) && found != null) return found;
            var robot = new Material(source) { name = "Robot " + source.name, hideFlags = HideFlags.DontSave };
            Color colour = source.GetColor("_BaseColor");
            bool glowing = source.IsKeywordEnabled("_EMISSION");
            bool clear = source.renderQueue >= (int)RenderQueue.Transparent;
            float lightness = colour.r * 0.3f + colour.g * 0.59f + colour.b * 0.11f;
            Color metal = lightness < 0.2f ? FinalePalette.DarkFrame : lightness < 0.42f ? FinalePalette.Frame : lightness < 0.62f ? FinalePalette.Steel : FinalePalette.Armour;
            Color result = Color.Lerp(metal, colour, keep);
            if (glowing)
            {
                result = accent;
                robot.SetColor("_EmissionColor", accent * 1.4f);
            }
            if (clear) result = new Color(accent.r, accent.g, accent.b, colour.a);
            else result.a = colour.a;
            robot.SetColor("_BaseColor", result);
            if (robot.HasProperty("_Metallic")) robot.SetFloat("_Metallic", glowing ? 0f : 0.55f);
            if (robot.HasProperty("_Smoothness")) robot.SetFloat("_Smoothness", glowing ? 0f : 0.38f);
            Cache[(source, accent)] = robot;
            return robot;
        }

        // An antenna with a blinking light on top of the head, a glowing visor across the face, and a ring of rivets.
        private void AddParts()
        {
            Bounds bounds = Measure();
            if (bounds.size.sqrMagnitude < 0.01f) return;
            Transform head = FindHead();
            float scale = Mathf.Clamp(bounds.size.y / 3f, 0.4f, 3f);
            Vector3 top = head != null ? head.position + Vector3.up * scale * 0.35f : new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            Transform anchor = head != null ? head : transform;

            var parts = new GameObject("Robot Parts").AddComponent<RobotPart>().transform;
            parts.SetParent(anchor, true);
            parts.position = top;
            parts.rotation = transform.rotation;
            parts.localScale = Vector3.one; // lossy scale cancels below
            float s = scale / Mathf.Max(0.01f, anchor.lossyScale.y);
            Color light = accent;
            MudArt.Part(parts, "Antenna", Vector3.zero, Quaternion.Euler(0f, 0f, 12f), "robot_antenna", k =>
            {
                k.Rod(Vector3.zero, new Vector3(0f, 0.55f, 0f), 0.025f, 5, FinalePalette.Steel);
                k.Prism(new Vector3(0f, -0.05f, 0f), Quaternion.identity, 0.09f, 0.07f, 0.1f, 8, FinalePalette.DarkFrame);
            }).localScale = Vector3.one * s;
            antennaLight = MudArt.Part(parts, "Antenna Light", Quaternion.Euler(0f, 0f, 12f) * new Vector3(0f, 0.6f * s, 0f), Quaternion.identity, "robot_antenna_light_" + ColorUtility.ToHtmlStringRGB(light),
                k => k.Ball(Vector3.zero, 0.07f, light, 1, 2.5f), false);
            antennaLight.localScale = Vector3.one * s;
            Transform visor = MudArt.Part(parts, "Visor", new Vector3(0f, -0.3f * s, 0f) + parts.InverseTransformDirection(transform.forward) * 0.32f * s, Quaternion.identity,
                "robot_visor_" + ColorUtility.ToHtmlStringRGB(light), k => k.Box(Vector3.zero, new Vector3(0.5f, 0.08f, 0.06f), light, 1.6f), false);
            visor.localScale = Vector3.one * s;
            visor.rotation = transform.rotation;
            // Rivets in a ring round the middle of the body.
            var rivets = new GameObject("Rivets").AddComponent<RobotPart>().transform;
            rivets.SetParent(transform, true);
            rivets.position = bounds.center;
            rivets.rotation = transform.rotation;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.85f;
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                MudArt.Part(rivets, "Rivet", new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius / Mathf.Max(0.01f, transform.lossyScale.x), Quaternion.identity, "robot_rivet",
                    k => k.Ball(Vector3.zero, 0.06f, FinalePalette.Steel, 0)).localScale = Vector3.one * Mathf.Max(1f, scale) / Mathf.Max(0.01f, transform.lossyScale.x);
            }
        }

        private Transform FindHead()
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n == "head" || n.EndsWith(" head") || n.StartsWith("head ") || n == "head pivot") return t;
            }
            return null;
        }

        private Bounds Measure()
        {
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }
    }

    /// <summary>Marks the bolted-on robot bits so the repaint leaves them alone.</summary>
    public sealed class RobotPart : MonoBehaviour { }
}
