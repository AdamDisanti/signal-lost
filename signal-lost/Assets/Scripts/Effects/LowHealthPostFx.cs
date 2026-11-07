using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SignalLost.Effects
{
    /// <summary>
    /// The camera side of the low-health warning, on top of the HUD's red heartbeat edges: the world drains of colour
    /// and darkens toward a tight red vignette, and each heartbeat squeezes the view (vignette, colour fringing, a
    /// little lens pinch). <see cref="SignalLost.UI.GameHud"/> creates it and feeds it the level and the beat each frame.
    /// A global post-processing volume made at runtime, so no scene needs one.
    /// </summary>
    public sealed class LowHealthPostFx : MonoBehaviour
    {
        private Volume volume;
        private VolumeProfile profile;
        private Vignette vignette;
        private ColorAdjustments colour;
        private ChromaticAberration fringe;
        private LensDistortion pinch;

        public static LowHealthPostFx Create(Transform parent)
        {
            var go = new GameObject("Low Health Post FX");
            go.transform.SetParent(parent, false);
            return go.AddComponent<LowHealthPostFx>();
        }

        private void Awake()
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Low Health (runtime)";
            profile.hideFlags = HideFlags.DontSave;

            vignette = profile.Add<Vignette>(true);
            vignette.color.Override(new Color(0.45f, 0f, 0f));
            vignette.smoothness.Override(0.55f);
            vignette.rounded.Override(false);

            colour = profile.Add<ColorAdjustments>(true);
            colour.colorFilter.Override(new Color(1f, 0.82f, 0.82f));

            fringe = profile.Add<ChromaticAberration>(true);
            pinch = profile.Add<LensDistortion>(true);

            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f; // over the scene's own look and the drunk effect
            volume.sharedProfile = profile;
            volume.weight = 0f;
            Set(0f, 0f);
        }

        /// <param name="level">0 healthy to 1 nearly dead (already eased by the HUD).</param>
        /// <param name="beat">The heartbeat pulse, 0 to about 1.</param>
        public void Set(float level, float beat)
        {
            level = Mathf.Clamp01(level);
            if (volume == null) return;
            volume.weight = level > 0.001f ? 1f : 0f;
            volume.enabled = level > 0.001f;
            if (!volume.enabled) return;

            vignette.intensity.Override(Mathf.Lerp(0.25f, 0.5f, level) + 0.12f * beat * level);
            colour.saturation.Override(-55f * level);
            colour.postExposure.Override(-0.35f * level);
            colour.contrast.Override(12f * level);
            fringe.intensity.Override(Mathf.Clamp01(0.15f * level + 0.45f * beat * level));
            pinch.intensity.Override(-0.08f * beat * level);
        }

        private void OnDestroy()
        {
            if (profile != null) Destroy(profile);
        }
    }
}
