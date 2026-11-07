using UnityEngine;

namespace SignalLost.Combat
{
    /// <summary>
    /// The glowing parts inside the mining laser's see-through charge chamber: a core rod, a row of coil rings, the
    /// emitter lens and three charge lights on top. It has no point light: one inside the chamber sat so close to
    /// the parts that it blew the held gun out to white.
    /// <see cref="MiningLaser"/> drives it every frame while the laser is held: the coils light up one by one from the
    /// back as a shot charges, the core brightens and flickers, and at full charge everything pulses white-hot. Firing
    /// flashes it, then it dims and recovers over the shot cooldown. When nothing drives it (the pickup lying in the
    /// wreck) it idles with a slow, dim breathing glow. Built by the MiningLaserModelBuilder editor script.
    /// </summary>
    public sealed class LaserChargeVisual : MonoBehaviour
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer core;
        [Tooltip("Back to front: they light up in this order as the shot charges.")]
        [SerializeField] private Renderer[] coils;
        [SerializeField] private Renderer lens;
        [Tooltip("Back to front: one per third of the charge.")]
        [SerializeField] private Renderer[] indicators;
        [SerializeField] private Color energy = new Color(0.3f, 0.95f, 1f);
        [SerializeField] private Color hot = new Color(0.6f, 0.97f, 1f);

        private MaterialPropertyBlock block;
        private int drivenFrame = -10;
        private float fireFlash;

        /// <summary>Wires the parts; called by the editor builder.</summary>
        public void Setup(Renderer core, Renderer[] coils, Renderer lens, Renderer[] indicators)
        {
            this.core = core;
            this.coils = coils;
            this.lens = lens;
            this.indicators = indicators;
        }

        /// <summary>Matches the glow to the laser's energy colour.</summary>
        public void SetEnergyColor(Color color)
        {
            energy = color;
            hot = Color.Lerp(color, Color.white, 0.45f);
        }

        /// <summary>A shot just left: a bright flash that fades.</summary>
        public void Fired() => fireFlash = 1f;

        /// <param name="charging">A shot is being charged.</param>
        /// <param name="charge01">0 to 1 while charging.</param>
        /// <param name="cooldown01">1 just after a shot, falling to 0 when the next one is ready.</param>
        public void Drive(bool charging, float charge01, float cooldown01)
        {
            drivenFrame = Time.frameCount;
            Apply(charging, Mathf.Clamp01(charge01), Mathf.Clamp01(cooldown01));
        }

        private void Update()
        {
            if (Time.frameCount - drivenFrame > 1) Apply(false, 0f, 0f); // idle when nobody holds it
        }

        private void Apply(bool charging, float charge, float cooldown)
        {
            block ??= new MaterialPropertyBlock();
            fireFlash = Mathf.MoveTowards(fireFlash, 0f, Time.deltaTime / 0.25f);
            float t = Time.time;
            bool full = charging && charge >= 1f;

            // Resting glow: slow breathing, dimmed while the laser recovers from a shot.
            float idle = (0.35f + 0.1f * Mathf.Sin(t * 2.2f)) * (1f - 0.8f * cooldown);
            float coreLevel;
            Color coreColor;
            if (full)
            {
                coreLevel = 1.6f + 0.4f * Mathf.Sin(t * 28f) + Random.Range(-0.2f, 0.2f);
                coreColor = hot;
            }
            else if (charging)
            {
                coreLevel = Mathf.Lerp(0.5f, 1.4f, charge) * Random.Range(0.85f, 1.1f);
                coreColor = Color.Lerp(energy, hot, charge * 0.4f);
            }
            else
            {
                coreLevel = idle;
                coreColor = energy;
            }
            coreLevel += fireFlash * 2f;
            if (fireFlash > 0f) coreColor = Color.Lerp(coreColor, hot, fireFlash);
            Set(core, coreColor, coreLevel);

            // Coils: each one lights as the charge passes it, with a short fade-in, and buzzes while lit.
            if (coils != null)
                for (int i = 0; i < coils.Length; i++)
                {
                    float level;
                    if (full) level = 1.2f + 0.4f * Mathf.Sin(t * 28f - i * 0.9f);
                    else if (charging)
                    {
                        float start = (float)i / coils.Length;
                        float lit = Mathf.Clamp01((charge - start) * coils.Length * 1.5f);
                        level = lit * (0.9f + 0.25f * Mathf.Sin(t * 40f + i * 1.7f)) + 0.06f;
                    }
                    else
                    {
                        // A soft wave runs along the coils at rest.
                        level = (0.12f + 0.12f * Mathf.Max(0f, Mathf.Sin(t * 3f - i * 0.8f))) * (1f - 0.8f * cooldown);
                    }
                    Set(coils[i], full ? hot : energy, level + fireFlash * 1.2f);
                }

            Set(lens, charging ? Color.Lerp(energy, hot, charge) : energy, (charging ? Mathf.Lerp(0.4f, 1.6f, charge) : idle * 1.2f) + fireFlash * 2f);

            // Charge lights on top: one per third; all three blink at full; a single amber one blinks while cooling.
            if (indicators != null)
                for (int i = 0; i < indicators.Length; i++)
                {
                    float level;
                    Color color = energy;
                    if (full) level = Mathf.Sin(t * 18f) > 0f ? 1.5f : 0.3f;
                    else if (charging) level = charge >= (i + 1f) / indicators.Length - 0.001f ? 1.3f : 0.12f;
                    else if (cooldown > 0f)
                    {
                        color = new Color(1f, 0.55f, 0.15f);
                        level = i == 0 && Mathf.Sin(t * 12f) > 0f ? 1.1f : 0.1f;
                    }
                    else level = 0.8f; // ready
                    Set(indicators[i], color, level);
                }

        }

        private void Set(Renderer part, Color color, float level)
        {
            if (part == null) return;
            part.GetPropertyBlock(block);
            block.SetColor(EmissionId, color * Mathf.Max(0f, level));
            block.SetColor(BaseColorId, Color.Lerp(color * 0.35f, color, Mathf.Clamp01(level * 0.5f)));
            part.SetPropertyBlock(block);
        }
    }
}
