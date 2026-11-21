using SignalLost.Effects;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// A tall soft pillar of light over a quest objective, seen from far off over the trees (the cursed lanterns' green
    /// beams, the spirit gongs' teal lights). A vertical band that turns about its own axis to face the camera, bright at
    /// the base and fading upward, with a point light at its foot. Made at runtime by the quest pieces; nothing is saved.
    /// </summary>
    public sealed class QuestBeacon : MonoBehaviour
    {
        private Color color;
        private float height, width, strength, target, pulse;
        private Mesh mesh;
        private Light glow;
        private MeshRenderer view;
        private Color32[] colors;
        private float lightIntensity;

        /// <summary>0 = off, 1 = full. Fades there.</summary>
        public float Strength
        {
            get => target;
            set => target = Mathf.Clamp01(value);
        }

        /// <summary>A beacon at <paramref name="localBase"/> under <paramref name="parent"/>, starting off.</summary>
        public static QuestBeacon Attach(Transform parent, Vector3 localBase, Color color, float height, float width, float lightRange = 8f,
            float lightIntensity = 2.5f, float pulse = 0.12f)
        {
            var go = new GameObject("Quest Beacon") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localBase;
            var beacon = go.AddComponent<QuestBeacon>();
            beacon.Build(color, height, width, lightRange, lightIntensity, pulse);
            return beacon;
        }

        public void SetColor(Color value)
        {
            color = value;
            if (glow != null) glow.color = value;
        }

        private void Build(Color tint, float tall, float wide, float lightRange, float intensity, float pulseAmount)
        {
            color = tint;
            height = tall;
            width = wide;
            pulse = pulseAmount;
            lightIntensity = intensity;

            // Three rows (base, low, top): bright up to 12% of the height, then fading out to the top.
            mesh = new Mesh { name = "Beacon", hideFlags = HideFlags.DontSave };
            float[] rows = { 0f, 0.12f, 1f };
            var vertices = new Vector3[6];
            var uvs = new Vector2[6];
            for (int r = 0; r < 3; r++)
                for (int s = 0; s < 2; s++)
                {
                    vertices[r * 2 + s] = new Vector3((s - 0.5f) * width, rows[r] * height, 0f);
                    uvs[r * 2 + s] = new Vector2(rows[r], s); // the beam texture's band runs across v
                }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3, 2, 4, 3, 3, 4, 5 };
            colors = new Color32[6];
            mesh.colors32 = colors;
            mesh.bounds = new Bounds(new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, width));

            var band = new GameObject("Band") { hideFlags = HideFlags.DontSave };
            band.transform.SetParent(transform, false);
            band.AddComponent<MeshFilter>().sharedMesh = mesh;
            view = band.AddComponent<MeshRenderer>();
            view.sharedMaterial = EffectMaterials.Additive(EffectMaterials.Look.Beam);
            view.shadowCastingMode = ShadowCastingMode.Off;
            view.receiveShadows = false;

            if (lightRange > 0f)
            {
                glow = new GameObject("Beacon Light") { hideFlags = HideFlags.DontSave }.AddComponent<Light>();
                glow.transform.SetParent(transform, false);
                glow.transform.localPosition = Vector3.up * 0.8f;
                glow.type = LightType.Point;
                glow.range = lightRange;
                glow.color = color;
                glow.shadows = LightShadows.None;
                glow.intensity = 0f;
            }
            Apply();
        }

        private void LateUpdate()
        {
            if (!Mathf.Approximately(strength, target)) strength = Mathf.MoveTowards(strength, target, Time.deltaTime * 1.5f);
            Camera eye = Camera.main;
            if (eye != null)
            {
                Vector3 look = eye.transform.position - transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) view.transform.rotation = Quaternion.LookRotation(look);
            }
            Apply();
        }

        private void Apply()
        {
            float k = strength * (1f - pulse + pulse * Mathf.Sin(Time.time * 2.4f + transform.position.x));
            view.enabled = k > 0.01f;
            if (glow != null)
            {
                glow.intensity = lightIntensity * k;
                glow.enabled = k > 0.01f;
            }
            if (!view.enabled) return;
            byte a0 = (byte)(255f * Mathf.Clamp01(k * 0.9f)), a1 = (byte)(255f * Mathf.Clamp01(k * 0.75f));
            Color32 c = color;
            for (int s = 0; s < 2; s++)
            {
                colors[s] = new Color32(c.r, c.g, c.b, a0);
                colors[2 + s] = new Color32(c.r, c.g, c.b, a1);
                colors[4 + s] = new Color32(c.r, c.g, c.b, 0);
            }
            mesh.colors32 = colors;
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
