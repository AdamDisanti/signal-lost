using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Effects
{
    /// <summary>
    /// Shared particle textures and materials for the code-built effects (<see cref="FireEffect"/>,
    /// <see cref="ParticleBurst"/>). The textures (a flame tongue, a soft glow, a smoke puff) are drawn in code, so
    /// there are no image files. The materials start from the two URP particle materials in
    /// Resources/SignalLostEffects so their shader variants are included in builds; if those are missing they are made
    /// from the URP particle shader directly (fine in the Editor).
    /// </summary>
    public static class EffectMaterials
    {
        public enum Look { Flame, Glow, Smoke }

        private const string AdditivePath = "SignalLostEffects/ParticleAdditive";
        private const string AlphaPath = "SignalLostEffects/ParticleAlpha";

        private static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();
        private static Texture2D flame, glow, smoke;

        /// <summary>Glowing, added on top of what is behind it: flames, embers, sparks, glows.</summary>
        public static Material Additive(Look look) => Get(look, true);

        /// <summary>Ordinary see-through: smoke, dust, sand.</summary>
        public static Material AlphaBlended(Look look) => Get(look, false);

        public static Texture2D Texture(Look look)
        {
            switch (look)
            {
                case Look.Flame: return flame != null ? flame : flame = Draw("Fire Tongue", FlameAlpha);
                case Look.Smoke: return smoke != null ? smoke : smoke = Draw("Smoke Puff", SmokeAlpha);
                default: return glow != null ? glow : glow = Draw("Soft Glow", GlowAlpha);
            }
        }

        private static Material Get(Look look, bool additive)
        {
            int key = (int)look * 2 + (additive ? 1 : 0);
            if (Cache.TryGetValue(key, out Material cached) && cached != null) return cached;

            Material material;
            var template = Resources.Load<Material>(additive ? AdditivePath : AlphaPath);
            if (template != null) material = new Material(template);
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                material = new Material(shader);
                Configure(material, additive);
            }
            material.name = (additive ? "Additive " : "Alpha ") + look;
            material.hideFlags = HideFlags.DontSave; // shared across scenes; never written to disk
            Texture2D texture = Texture(look);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            Cache[key] = material;
            return material;
        }

        /// <summary>The same settings the Resources materials are saved with.</summary>
        private static void Configure(Material material, bool additive)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        // ---------- Textures ----------

        private delegate float Alpha(float x, float y);

        private static Texture2D Draw(string name, Alpha alpha)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[size * size];
            for (int j = 0; j < size; j++)
                for (int i = 0; i < size; i++)
                {
                    // x from -1 (left) to 1 (right), y from -1 (bottom) to 1 (top).
                    float x = (i + 0.5f) / size * 2f - 1f;
                    float y = (j + 0.5f) / size * 2f - 1f;
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(x, y)) * 255f);
                    pixels[j * size + i] = new Color32(255, 255, 255, a);
                }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        // A tongue of flame: round at the bottom, drawn out into a point at the top.
        private static float FlameAlpha(float x, float y)
        {
            float v = (y + 1f) * 0.5f;                       // 0 at the bottom, 1 at the top
            float width = 0.62f * Mathf.Pow(1f - v, 0.55f) * Mathf.Clamp01(v * 3.2f + 0.25f);
            if (width <= 0.001f) return 0f;
            float dx = x / width;
            float dy = v < 0.28f ? (0.28f - v) / 0.3f : 0f;   // rounded base
            float d = dx * dx + dy * dy;
            float body = Mathf.Clamp01(1f - d);
            return body * body * Mathf.Clamp01((1f - v) * 4f);
        }

        // A soft round glow, bright in the middle.
        private static float GlowAlpha(float x, float y)
        {
            float d = Mathf.Clamp01(Mathf.Sqrt(x * x + y * y));
            float k = 1f - d;
            return k * k * (0.6f + 0.4f * k);
        }

        // A lumpy puff of smoke.
        private static float SmokeAlpha(float x, float y)
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float lumps = 0.75f + 0.25f * Mathf.PerlinNoise(x * 2.3f + 7.1f, y * 2.3f + 3.4f)
                                 + 0.15f * Mathf.PerlinNoise(x * 5.1f + 1.3f, y * 5.1f + 9.2f);
            float k = Mathf.Clamp01(1f - d / Mathf.Max(0.3f, lumps * 0.95f));
            return k * k * 0.9f;
        }
    }
}
