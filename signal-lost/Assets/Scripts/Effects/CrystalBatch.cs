using UnityEngine;

namespace SignalLost.Effects
{
    /// <summary>
    /// A group of glowing cave crystals that share one colour and one light. Pick a crystal material in the
    /// Inspector and every crystal under this object switches to it, and the glow light takes the material's
    /// emission colour. Add, move or delete crystals freely; the light is the only light source a cave needs.
    /// </summary>
    public sealed class CrystalBatch : MonoBehaviour
    {
        [Tooltip("One of the Crystal materials (Prefabs/Enviornment objects/CrystalCaves/Materials).")]
        [SerializeField] private Material crystalMaterial;
        [SerializeField] private Light glow;

        public Material CrystalMaterial => crystalMaterial;

        private void OnValidate() => Apply();

        /// <summary>Puts the material on every crystal and matches the light to it.</summary>
        public void Apply()
        {
            if (crystalMaterial == null) return;
            foreach (MeshRenderer crystal in GetComponentsInChildren<MeshRenderer>(true)) crystal.sharedMaterial = crystalMaterial;
            if (glow != null) glow.color = GlowColor(crystalMaterial);
        }

        /// <summary>The material's emission colour at full brightness (the light's intensity sets how bright).</summary>
        public static Color GlowColor(Material material)
        {
            Color emission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.white;
            float brightest = emission.maxColorComponent;
            return brightest > 0f ? new Color(emission.r / brightest, emission.g / brightest, emission.b / brightest) : Color.white;
        }
    }
}
