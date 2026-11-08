using System.Collections.Generic;
using UnityEngine;

namespace SignalLost.Effects
{
    /// <summary>
    /// Gives a cave its own light inside a sunlit level: while the camera is under the cave's roof collider, the sun,
    /// sky light, reflections and fog fade to the cave settings over <see cref="blendDistance"/> metres from the
    /// entrance, to a bright, even cave fill that the glowing crystals tint. The scene's own lighting settings are the
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
        [Tooltip("The cave's fill light, bright enough that every part of the cave is easy to see and navigate; the crystals add colour on top.")]
        [SerializeField] private Color skyLight = new Color(0.72f, 0.76f, 0.92f);     // lights floors
        [SerializeField] private Color equatorLight = new Color(0.56f, 0.6f, 0.74f);  // walls
        [SerializeField] private Color groundLight = new Color(0.42f, 0.44f, 0.55f);  // ceilings
        [SerializeField] private Color fogColor = new Color(0.15f, 0.17f, 0.24f);
        [SerializeField, Min(0f)] private float fogStart = 60f;
        [SerializeField, Min(1f)] private float fogEnd = 420f;

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
