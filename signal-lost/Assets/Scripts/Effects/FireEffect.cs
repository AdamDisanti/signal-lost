using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SignalLost.Effects
{
    public enum FireShape
    {
        /// <summary>A patch of fire burning on the ground: flames rise from a circle.</summary>
        Ground,
        /// <summary>A jet of flame blowing out along the object's forward (+Z) axis: exhausts, flamethrowers, torches.</summary>
        Jet,
    }

    /// <summary>
    /// A general-purpose fire made of particles: licking flame tongues, a white-hot core, a glowing bed (ground fires),
    /// rising embers, smoke and a flickering light. It is built entirely in code, so there is nothing to set up: add it
    /// to any GameObject (or drop in one of the prefabs in Prefabs/Effects, or use GameObject > Signal Lost > Fire)
    /// and set its size. It previews live in the Scene view.
    ///
    /// Level design: place it and leave Burn Time at 0 to burn forever. Gameplay code: use <see cref="Spawn"/> or
    /// <see cref="SpawnJet"/>, drive <see cref="Strength"/> to grow/fade it, and call <see cref="Extinguish"/> to let it
    /// die down. It does no damage on its own (the desert mini-bosses' FireZone adds that).
    ///
    /// Only a few fires at once get a real light (<see cref="MaxLights"/>), so a field of fire stays cheap.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Signal Lost/Effects/Fire Effect")]
    public sealed class FireEffect : MonoBehaviour
    {
        /// <summary>Most fires lit by a real point light at once; the rest glow without one.</summary>
        public const int MaxLights = 8;

        [Header("Size")]
        [SerializeField] private FireShape shape = FireShape.Ground;
        [Tooltip("Ground: radius of the burning patch. Jet: radius of the nozzle.")]
        [SerializeField, Min(0.05f)] private float radius = 0.6f;
        [Tooltip("Ground: how tall the flames reach. Jet: how long the flame is.")]
        [SerializeField, Min(0.1f)] private float height = 1.4f;
        [Tooltip("How fierce it is: flame density, brightness and light. 0 is out.")]
        [SerializeField, Range(0f, 2f)] private float intensity = 1f;

        [Header("Look")]
        [Tooltip("Multiplies the flame colours. White is an ordinary orange fire.")]
        [SerializeField] private Color tint = Color.white;
        [SerializeField] private bool smoke = true;
        [SerializeField, Range(0f, 1f)] private float smokeAmount = 0.5f;
        [SerializeField] private Color smokeColor = new Color(0.16f, 0.15f, 0.14f, 0.55f);
        [SerializeField] private bool embers = true;
        [Tooltip("A glowing bed of coals under a ground fire.")]
        [SerializeField] private bool groundGlow = true;

        [Header("Light")]
        [SerializeField] private bool castLight = true;
        [SerializeField] private Color lightColor = new Color(1f, 0.55f, 0.2f);
        [SerializeField, Min(0f)] private float lightIntensity = 2.5f;

        [Header("Lifetime")]
        [Tooltip("Seconds it burns before dying down by itself. 0 burns until told to stop.")]
        [SerializeField, Min(0f)] private float burnTime;
        [Tooltip("Remove the GameObject once it has died down (for spawned fires).")]
        [SerializeField] private bool destroyWhenOut;

        private static int lightsInUse;

        private readonly List<ParticleSystem> systems = new List<ParticleSystem>();
        private readonly List<float> baseRates = new List<float>();
        private Transform fxRoot;
        private Light fireLight;
        private bool built, dirty, holdsLight, extinguishing;
        private float age, outAt, longestLife, lightBase;
        private float strength = 1f;

        /// <summary>0 to 1: scales density, flame size and light. Use it to grow a fire in or fade it out.</summary>
        public float Strength
        {
            get => strength;
            set => strength = Mathf.Clamp01(value);
        }

        public float Radius => radius;
        public float Height => height;
        public bool IsBurning => built && !extinguishing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => lightsInUse = 0;

        // ---------- Spawning from code ----------

        /// <summary>A ground fire at a point. Burn Time 0 burns until <see cref="Extinguish"/>; it removes itself once out.</summary>
        /// <param name="height">Flame height; 0 or less picks one that suits the radius.</param>
        /// <param name="tint">Multiplies the flame colours (null: ordinary fire).</param>
        /// <param name="smokeAmount">0 to 1; less than 0 keeps the default.</param>
        /// <param name="fierceness">Flame density and light (the Intensity setting).</param>
        public static FireEffect Spawn(Vector3 position, float radius, float height = 0f, float burnTime = 0f, Transform parent = null,
            Color? tint = null, float smokeAmount = -1f, float fierceness = 1f)
        {
            var go = new GameObject("Fire");
            go.SetActive(false); // configure before it builds
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var fire = go.AddComponent<FireEffect>();
            fire.shape = FireShape.Ground;
            fire.radius = Mathf.Max(0.05f, radius);
            fire.height = height > 0f ? height : HeightFor(radius);
            fire.burnTime = Mathf.Max(0f, burnTime);
            fire.destroyWhenOut = true;
            if (tint.HasValue) fire.tint = tint.Value;
            if (smokeAmount >= 0f) fire.smokeAmount = Mathf.Clamp01(smokeAmount);
            fire.intensity = Mathf.Clamp(fierceness, 0f, 2f);
            go.SetActive(true);
            return fire;
        }

        /// <summary>A jet of flame pointing along the parent's local direction (an exhaust, a torch).</summary>
        public static FireEffect SpawnJet(Transform parent, Vector3 localPosition, Quaternion localRotation, float radius, float length)
        {
            var go = new GameObject("Fire Jet");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            var fire = go.AddComponent<FireEffect>();
            fire.shape = FireShape.Jet;
            fire.radius = Mathf.Max(0.05f, radius);
            fire.height = Mathf.Max(0.1f, length);
            fire.groundGlow = false;
            fire.smokeAmount = 0.35f;
            go.SetActive(true);
            return fire;
        }

        /// <summary>A flame height that looks right for a patch of this radius.</summary>
        public static float HeightFor(float radius) => Mathf.Clamp(0.8f + radius * 0.9f, 0.7f, 3.5f);

        /// <summary>Changes the size (and shape) and rebuilds it.</summary>
        public void Configure(float newRadius, float newHeight, FireShape newShape)
        {
            radius = Mathf.Max(0.05f, newRadius);
            height = Mathf.Max(0.1f, newHeight);
            shape = newShape;
            if (built) Build();
        }

        public void SetTint(Color color)
        {
            tint = color;
            if (built) Build();
        }

        /// <summary>Stops new flames; it dies down over a second or two, then (if set) removes itself.</summary>
        public void Extinguish()
        {
            if (extinguishing) return;
            extinguishing = true;
            foreach (ParticleSystem system in systems)
                if (system != null) system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            outAt = Time.time + longestLife;
        }

        /// <summary>Lights it again after <see cref="Extinguish"/>.</summary>
        public void Reignite()
        {
            extinguishing = false;
            age = 0f;
            foreach (ParticleSystem system in systems)
                if (system != null) system.Play(false);
        }

        // ---------- Unity ----------

        private void OnEnable()
        {
            Build();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.update -= EditorPreview;
                EditorApplication.update += EditorPreview;
            }
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorPreview;
#endif
            ReleaseLight();
            // Edit-mode previews are hidden, unsaved objects: always clean them up.
            if (!Application.isPlaying) Clear();
        }

        private void OnDestroy() => ReleaseLight();

        private void OnValidate() => dirty = true;

        private void RebuildIfDirty()
        {
            if (!dirty) return;
            dirty = false;
            if (built) Build();
        }

        private void Update()
        {
            RebuildIfDirty();
            if (!built) return;
            if (!Application.isPlaying)
            {
                ApplyStrength();
                return;
            }

            age += Time.deltaTime;
            if (!extinguishing && burnTime > 0f && age >= burnTime) Extinguish();
            ApplyStrength();
            if (extinguishing && Time.time >= outAt && destroyWhenOut) Destroy(gameObject);
        }

        // ---------- Building ----------

        private HideFlags ChildFlags => Application.isPlaying ? HideFlags.None : HideFlags.HideAndDontSave;

        private void Clear()
        {
            ReleaseLight();
            systems.Clear();
            baseRates.Clear();
            fireLight = null;
            if (fxRoot != null)
            {
                if (Application.isPlaying) Destroy(fxRoot.gameObject);
                else DestroyImmediate(fxRoot.gameObject);
            }
            fxRoot = null;
            built = false;
        }

        private void Build()
        {
            Clear();
            var root = new GameObject("Fire FX") { hideFlags = ChildFlags };
            fxRoot = root.transform;
            fxRoot.SetParent(transform, false);
            longestLife = 0.5f;
            extinguishing = false;
            age = 0f;

            if (shape == FireShape.Jet) BuildJet();
            else BuildGround();

            if (castLight) BuildLight();
            built = true;

            bool prewarm = Application.isPlaying && burnTime <= 0f && !destroyWhenOut;
            foreach (ParticleSystem system in systems)
            {
                var main = system.main;
                main.prewarm = prewarm;
                if (Application.isPlaying) system.Play(false);
            }
#if UNITY_EDITOR
            if (!Application.isPlaying)
                foreach (ParticleSystem system in systems) system.Simulate(1.5f, false, true, false);
#endif
            ApplyStrength();
        }

        private void BuildGround()
        {
            float r = radius, h = height;
            float life = Mathf.Clamp(0.35f + 0.32f * h, 0.45f, 1.5f);
            float rise = h / life;
            float tongue = Mathf.Clamp(r * 0.75f + 0.25f, 0.3f, 2.2f);

            // Flame tongues: tall, licking, shrinking and reddening as they rise.
            ParticleSystem flames = NewSystem("Flames", EffectMaterials.Additive(EffectMaterials.Look.Flame), 220, 2,
                Mathf.Clamp(14f + 30f * r, 10f, 110f), tongue);
            var main = flames.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.65f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(rise * 0.55f, rise * 0.95f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(tongue * 0.7f, tongue * 1.1f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(tongue * 1.3f, tongue * 2f);
            main.startSizeZ = 1f;
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            main.startColor = tint;
            UpCone(flames, r * 0.85f, 4f);
            SizeOverLife(flames, new Keyframe(0f, 0.45f), new Keyframe(0.2f, 1f), new Keyframe(0.7f, 0.6f), new Keyframe(1f, 0.1f));
            ColorOverLife(flames, FlameGradient());
            Noise(flames, 0.25f + r * 0.15f, 1.3f, 1.4f);
            SetupRenderer(flames, ParticleSystemRenderMode.VerticalBillboard, 2);
            longestLife = Mathf.Max(longestLife, life);

            // White-hot core low in the fire.
            float coreSize = tongue * 0.75f;
            ParticleSystem core = NewSystem("Core", EffectMaterials.Additive(EffectMaterials.Look.Flame), 120, 3,
                Mathf.Clamp(10f + 20f * r, 8f, 70f), coreSize);
            main = core.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.35f, life * 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(rise * 0.3f, rise * 0.55f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(coreSize * 0.7f, coreSize);
            main.startSizeY = new ParticleSystem.MinMaxCurve(coreSize * 1.1f, coreSize * 1.6f);
            main.startSizeZ = 1f;
            main.startColor = tint;
            UpCone(core, r * 0.55f, 2f);
            SizeOverLife(core, new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.2f));
            ColorOverLife(core, CoreGradient());
            SetupRenderer(core, ParticleSystemRenderMode.VerticalBillboard, 3);

            if (groundGlow)
            {
                // A flickering bed of coals: big soft glows lying flat on the ground.
                float bed = r * 2.6f + 0.4f;
                ParticleSystem glow = NewSystem("Glow", EffectMaterials.Additive(EffectMaterials.Look.Glow), 16, 1, 7f, bed);
                glow.transform.localPosition = Vector3.up * 0.04f;
                main = glow.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(bed * 0.85f, bed * 1.05f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = tint * new Color(1f, 0.42f, 0.12f, 0.45f);
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                var shapeModule = glow.shape;
                shapeModule.shapeType = ParticleSystemShapeType.Sphere;
                shapeModule.radius = 0.01f;
                ColorOverLife(glow, Fade(0.3f, 0.7f));
                SetupRenderer(glow, ParticleSystemRenderMode.HorizontalBillboard, 1);
            }

            if (embers) BuildEmbers(r, rise, 1f);
            if (smoke && smokeAmount > 0f) BuildSmoke(r, h, rise, Vector3.up * h * 0.55f, false);
        }

        private void BuildJet()
        {
            float r = radius, length = height;
            float life = Mathf.Clamp(0.12f + 0.08f * length, 0.15f, 0.5f);
            float speed = length / life;
            float puff = Mathf.Clamp(r * 2.2f, 0.15f, 2f);

            // The flame: puffs that leave the nozzle small and bright, swell and redden, then vanish.
            ParticleSystem flames = NewSystem("Jet Flame", EffectMaterials.Additive(EffectMaterials.Look.Glow), 260, 2,
                Mathf.Clamp(50f + 60f * r * length, 40f, 220f), puff);
            var main = flames.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.85f, speed * 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(puff * 0.7f, puff * 1.1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = tint;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            ForwardCone(flames, r * 0.6f, 7f);
            SizeOverLife(flames, new Keyframe(0f, 0.35f), new Keyframe(0.5f, 1.15f), new Keyframe(1f, 1.5f));
            ColorOverLife(flames, FlameGradient());
            SetupRenderer(flames, ParticleSystemRenderMode.Billboard, 2);
            longestLife = Mathf.Max(longestLife, life);

            // A blue-white cone right at the nozzle.
            ParticleSystem core = NewSystem("Jet Core", EffectMaterials.Additive(EffectMaterials.Look.Glow), 80, 3,
                Mathf.Clamp(40f + 30f * r, 30f, 120f), puff * 0.6f);
            main = core.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.25f, life * 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed * 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(puff * 0.45f, puff * 0.6f);
            main.startColor = tint * new Color(0.85f, 0.92f, 1f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            ForwardCone(core, r * 0.35f, 3f);
            ColorOverLife(core, Fade(0.05f, 0.4f));
            SetupRenderer(core, ParticleSystemRenderMode.Billboard, 3);

            if (embers) BuildEmbers(r, speed * 0.4f, 0.6f, true);
            if (smoke && smokeAmount > 0f) BuildSmoke(r, length, speed * 0.25f, Vector3.forward * length * 0.8f, true);
        }

        private void BuildEmbers(float r, float speed, float amount, bool forward = false)
        {
            ParticleSystem sparks = NewSystem("Embers", EffectMaterials.Additive(EffectMaterials.Look.Glow), 80, 4,
                Mathf.Clamp((4f + 10f * r) * amount, 2f, 40f), 0.06f);
            var main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed * 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.08f);
            main.startColor = tint * new Color(1f, 0.75f, 0.35f, 1f);
            main.gravityModifier = forward ? 0.15f : -0.05f;
            if (forward) ForwardCone(sparks, r * 0.5f, 18f);
            else UpCone(sparks, radius * 0.8f, 12f);
            ColorOverLife(sparks, EmberGradient());
            Noise(sparks, 0.9f, 1.8f, 0.8f);
            ParticleSystemRenderer renderer = SetupRenderer(sparks, ParticleSystemRenderMode.Stretch, 4);
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1.5f;
            longestLife = Mathf.Max(longestLife, 1.9f);
        }

        private void BuildSmoke(float r, float h, float rise, Vector3 offset, bool trailing)
        {
            float puff = Mathf.Clamp(r * 1.4f + 0.4f, 0.4f, 3f);
            ParticleSystem smokeSystem = NewSystem("Smoke", EffectMaterials.AlphaBlended(EffectMaterials.Look.Smoke), 90, 0,
                Mathf.Clamp((3f + 7f * r) * smokeAmount * 2f, 1f, 30f), puff);
            smokeSystem.transform.localPosition = offset;
            var main = smokeSystem.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(trailing ? 0.2f : rise * 0.25f, trailing ? 0.6f : rise * 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(puff * 0.8f, puff * 1.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = smokeColor;
            main.gravityModifier = -0.04f; // keeps drifting up
            UpCone(smokeSystem, r * 0.6f, 10f);
            SizeOverLife(smokeSystem, new Keyframe(0f, 0.5f), new Keyframe(1f, 1.9f));
            ColorOverLife(smokeSystem, Fade(0.15f, 1f));
            var spin = smokeSystem.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            Noise(smokeSystem, 0.35f, 0.6f, 0.4f);
            SetupRenderer(smokeSystem, ParticleSystemRenderMode.Billboard, 0).sortMode = ParticleSystemSortMode.OldestInFront;
            longestLife = Mathf.Max(longestLife, 2.8f);
        }

        private void BuildLight()
        {
            if (Application.isPlaying)
            {
                if (lightsInUse >= MaxLights) return;
                lightsInUse++;
                holdsLight = true;
            }
            var go = new GameObject("Fire Light") { hideFlags = ChildFlags };
            go.transform.SetParent(fxRoot, false);
            go.transform.localPosition = shape == FireShape.Jet ? Vector3.forward * height * 0.3f : Vector3.up * height * 0.4f;
            fireLight = go.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.shadows = LightShadows.None;
            fireLight.range = Mathf.Clamp(radius * 4f + height * 2f, 3f, 16f);
            fireLight.color = lightColor * tint;
            lightBase = lightIntensity;
        }

        private void ReleaseLight()
        {
            if (!holdsLight) return;
            holdsLight = false;
            lightsInUse = Mathf.Max(0, lightsInUse - 1);
        }

        private void ApplyStrength()
        {
            float k = intensity * strength;
            for (int i = 0; i < systems.Count; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null) continue;
                var emission = system.emission;
                emission.rateOverTimeMultiplier = extinguishing ? 0f : baseRates[i] * k;
            }
            // A weak fire is a smaller fire: shrinks the emitters and the flames with them.
            if (fxRoot != null)
                fxRoot.localScale = Vector3.one * (Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(strength)) * Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(intensity)));
            if (fireLight == null) return;
            float t = Application.isPlaying ? Time.time : (float)RealTime();
            float flicker = 0.7f + 0.45f * Mathf.PerlinNoise(t * 9f, GetInstanceSeed()) - 0.15f * Mathf.PerlinNoise(t * 23f, 3.7f);
            float fade = extinguishing ? Mathf.Clamp01((outAt - Time.time) / Mathf.Max(0.1f, longestLife) * 2f - 1f) : 1f;
            fireLight.intensity = lightBase * k * flicker * fade;
        }

        private float seed = -1f;
        private float GetInstanceSeed()
        {
            if (seed < 0f) seed = Random.value * 100f;
            return seed;
        }

        private static double RealTime()
        {
#if UNITY_EDITOR
            return EditorApplication.timeSinceStartup;
#else
            return Time.realtimeSinceStartup;
#endif
        }

        // ---------- Particle system helpers ----------

        private ParticleSystem NewSystem(string name, Material material, int maxParticles, int order, float rate, float size)
        {
            var go = new GameObject(name) { hideFlags = ChildFlags };
            go.transform.SetParent(fxRoot, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 2f;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startSpeed = 1f;
            main.gravityModifier = 0f;
            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            systems.Add(system);
            baseRates.Add(rate);
            return system;
        }

        private static void UpCone(ParticleSystem system, float coneRadius, float angle)
        {
            var shapeModule = system.shape;
            shapeModule.enabled = true;
            shapeModule.shapeType = ParticleSystemShapeType.Cone;
            shapeModule.angle = angle;
            shapeModule.radius = Mathf.Max(0.01f, coneRadius);
            shapeModule.radiusThickness = 1f;
            shapeModule.rotation = new Vector3(-90f, 0f, 0f); // the cone's +Z turned to point up
        }

        private static void ForwardCone(ParticleSystem system, float coneRadius, float angle)
        {
            var shapeModule = system.shape;
            shapeModule.enabled = true;
            shapeModule.shapeType = ParticleSystemShapeType.Cone;
            shapeModule.angle = angle;
            shapeModule.radius = Mathf.Max(0.01f, coneRadius);
            shapeModule.radiusThickness = 1f;
            shapeModule.rotation = Vector3.zero;
        }

        private static void SizeOverLife(ParticleSystem system, params Keyframe[] keys)
        {
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(keys));
        }

        private static void ColorOverLife(ParticleSystem system, Gradient gradient)
        {
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void Noise(ParticleSystem system, float strength, float frequency, float scroll)
        {
            var noise = system.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = frequency;
            noise.scrollSpeed = scroll;
            noise.damping = true;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Medium;
        }

        private static ParticleSystemRenderer SetupRenderer(ParticleSystem system, ParticleSystemRenderMode mode, int order)
        {
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.maxParticleSize = 3f; // big flames right in front of the camera aren't clamped
            return renderer;
        }

        // ---------- Colours ----------

        private static Gradient FlameGradient() => Make(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.93f, 0.7f), 0f),
                new GradientColorKey(new Color(1f, 0.62f, 0.18f), 0.25f),
                new GradientColorKey(new Color(0.92f, 0.28f, 0.06f), 0.6f),
                new GradientColorKey(new Color(0.4f, 0.07f, 0.02f), 1f),
            },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.12f), new GradientAlphaKey(0.75f, 0.55f), new GradientAlphaKey(0f, 1f) });

        private static Gradient CoreGradient() => Make(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 0.92f), 0f),
                new GradientColorKey(new Color(1f, 0.85f, 0.45f), 0.5f),
                new GradientColorKey(new Color(1f, 0.55f, 0.15f), 1f),
            },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1f) });

        private static Gradient EmberGradient() => Make(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.55f), 0f),
                new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.5f),
                new GradientColorKey(new Color(0.6f, 0.1f, 0.03f), 1f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });

        /// <summary>White, fading in by <paramref name="fadeIn"/> and out from <paramref name="fadeOutFrom"/>.</summary>
        private static Gradient Fade(float fadeIn, float fadeOutFrom) => Make(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, Mathf.Clamp(fadeIn, 0.01f, 0.98f)),
                new GradientAlphaKey(1f, Mathf.Clamp(Mathf.Min(fadeOutFrom, 0.99f), fadeIn + 0.01f, 0.99f)), new GradientAlphaKey(0f, 1f),
            });

        private static Gradient Make(GradientColorKey[] colors, GradientAlphaKey[] alphas)
        {
            var gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            return gradient;
        }

        // ---------- Editor preview ----------

#if UNITY_EDITOR
        private double lastPreview;

        private void EditorPreview()
        {
            if (this == null || Application.isPlaying || !isActiveAndEnabled) return;
            RebuildIfDirty();
            if (!built) return;
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - lastPreview);
            if (dt < 1f / 30f) return;
            lastPreview = now;
            dt = Mathf.Min(dt, 0.1f);
            foreach (ParticleSystem system in systems)
                if (system != null) system.Simulate(dt, false, false, false);
            ApplyStrength();
            SceneView.RepaintAll();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            if (shape == FireShape.Jet)
            {
                Gizmos.DrawWireSphere(Vector3.zero, radius);
                Gizmos.DrawLine(Vector3.zero, Vector3.forward * height);
                return;
            }
            const int steps = 32;
            Vector3 previous = new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= steps; i++)
            {
                float a = i * Mathf.PI * 2f / steps;
                Vector3 next = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
            Gizmos.DrawLine(Vector3.zero, Vector3.up * height);
        }
#endif
    }
}
