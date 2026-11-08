using System.Collections.Generic;
using UnityEngine;

namespace SignalLost.Effects
{
    /// <summary>
    /// Makes a cave dark inside a sunlit level: while the camera is under the cave's roof collider, the sun,
    /// sky light, reflections and fog fade to the cave settings over <see cref="blendDistance"/> metres from the
    /// entrance, so the glowing crystals become the only light. The scene's own lighting settings are the
    /// outdoor look. Crystal lights deep inside (beyond <see cref="alwaysLitRadius"/> of the entrance) are only
    /// switched on while the player is in the cave, so they never shine through the mountain onto the outside.
    /// </summary>
    public sealed class CaveLighting : MonoBehaviour
    {
        [Tooltip("The cave's roof. Standing anywhere under it counts as being inside.")]
        [SerializeField] private Collider roof;
        [Tooltip("The cave mouth; the outdoor light fades out over Blend Distance from here.")]
        [SerializeField] private Transform entrance;
        [SerializeField, Min(1f)] private float blendDistance = 40f;
        [Tooltip("Empty: the scene's sun (Lighting settings).")]
        [SerializeField] private Light sun;
        [Tooltip("Parent of the crystal batches (their lights are managed here).")]
        [SerializeField] private Transform crystals;
        [SerializeField, Min(0f)] private float alwaysLitRadius = 70f;

        [Header("Inside the cave")]
        [Tooltip("A faint fill so rock shapes read between the crystals' pools of light.")]
        [SerializeField] private Color skyLight = new Color(0.09f, 0.1f, 0.16f);
        [SerializeField] private Color equatorLight = new Color(0.06f, 0.07f, 0.12f);
        [SerializeField] private Color groundLight = new Color(0.035f, 0.035f, 0.055f);
        [SerializeField] private Color fogColor = new Color(0.03f, 0.04f, 0.07f);
        [SerializeField, Min(0f)] private float fogStart = 25f;
        [SerializeField, Min(1f)] private float fogEnd = 260f;

        private Color outSky, outEquator, outGround, outFog;
        private float outFogStart, outFogEnd, outReflections, outSun;
        private readonly List<Light> deepLights = new List<Light>();
        private bool lightsOn = true;

        private void Awake()
        {
            if (sun == null) sun = RenderSettings.sun;
            outSky = RenderSettings.ambientSkyColor;
            outEquator = RenderSettings.ambientEquatorColor;
            outGround = RenderSettings.ambientGroundColor;
            outFog = RenderSettings.fogColor;
            outFogStart = RenderSettings.fogStartDistance;
            outFogEnd = RenderSettings.fogEndDistance;
            outReflections = RenderSettings.reflectionIntensity;
            outSun = sun != null ? sun.intensity : 0f;
            if (crystals != null && entrance != null)
                foreach (Light glow in crystals.GetComponentsInChildren<Light>(true))
                    if (Vector3.Distance(glow.transform.position, entrance.position) > alwaysLitRadius) deepLights.Add(glow);
        }

        private void LateUpdate()
        {
            Camera view = Camera.main;
            if (view == null || roof == null) return;
            Vector3 eye = view.transform.position;
            bool inside = roof.Raycast(new Ray(eye, Vector3.up), out _, 500f);
            float depth = !inside ? 0f : entrance == null ? 1f : Mathf.Clamp01(Vector3.Distance(eye, entrance.position) / blendDistance);
            Apply(depth * depth * (3f - 2f * depth));
            SetDeepLights(inside);
        }

        private void OnDisable()
        {
            Apply(0f);
            SetDeepLights(true);
        }

        private void Apply(float t)
        {
            RenderSettings.ambientSkyColor = Color.Lerp(outSky, skyLight, t);
            RenderSettings.ambientEquatorColor = Color.Lerp(outEquator, equatorLight, t);
            RenderSettings.ambientGroundColor = Color.Lerp(outGround, groundLight, t);
            RenderSettings.fogColor = Color.Lerp(outFog, fogColor, t);
            RenderSettings.fogStartDistance = Mathf.Lerp(outFogStart, fogStart, t);
            RenderSettings.fogEndDistance = Mathf.Lerp(outFogEnd, fogEnd, t);
            RenderSettings.reflectionIntensity = Mathf.Lerp(outReflections, 0f, t);
            if (sun != null)
            {
                sun.intensity = Mathf.Lerp(outSun, 0f, t);
                sun.enabled = t < 0.999f;
            }
        }

        private void SetDeepLights(bool on)
        {
            if (on == lightsOn) return;
            lightsOn = on;
            foreach (Light glow in deepLights) if (glow != null) glow.enabled = on;
        }
    }
}
