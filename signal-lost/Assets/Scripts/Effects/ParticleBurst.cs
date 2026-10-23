using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Effects
{
    /// <summary>
    /// One-shot particle bursts built in code, for hits and attacks: a spray of sand, a puff of smoke, a shower of
    /// sparks, a fireball. Uses the same textures and materials as <see cref="FireEffect"/>.
    ///
    /// There is one long-lived particle system per kind, and each burst is emitted into it. (Creating and destroying a
    /// GameObject and particle system for every burst caused hitches when an attack set off many at once.)
    /// </summary>
    public static class ParticleBurst
    {
        public enum Kind { Sand, Smoke, Sparks, Fireball }

        private const int MaxParticlesPerKind = 3000;
        private static readonly ParticleSystem[] Systems = new ParticleSystem[4];

        /// <param name="direction">Which way the burst flies (any length). Zero: in every direction.</param>
        /// <param name="size">Rough size of each puff in metres.</param>
        /// <param name="speed">Launch speed in metres per second.</param>
        /// <param name="spread">Half-angle of the cone the particles fly out in, in degrees.</param>
        /// <returns>The shared particle system the burst went into.</returns>
        public static ParticleSystem Spawn(Kind kind, Vector3 position, Vector3 direction, int count, float size, float speed,
            float spread = 25f, Color? color = null)
        {
            ParticleSystem system = SystemFor(kind);
            bool everywhere = direction.sqrMagnitude < 0.0001f;
            Quaternion aim = everywhere ? Quaternion.identity : Quaternion.LookRotation(direction.normalized);
            float cone = Mathf.Clamp(spread, 0f, 89f);
            Vector2 lifetime = Lifetime(kind);
            Color tint = color ?? DefaultColor(kind);
            var emit = new ParticleSystem.EmitParams();
            count = Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                Vector3 heading;
                Vector3 offset;
                if (everywhere)
                {
                    heading = Random.onUnitSphere;
                    offset = Random.insideUnitSphere * Mathf.Max(0.05f, size * 0.25f);
                }
                else
                {
                    // A random direction inside the cone, leaving from a small disc across its base.
                    float angle = Mathf.Acos(Mathf.Lerp(1f, Mathf.Cos(cone * Mathf.Deg2Rad), Random.value)) * Mathf.Rad2Deg;
                    Quaternion tilt = Quaternion.AngleAxis(Random.value * 360f, Vector3.forward) * Quaternion.AngleAxis(angle, Vector3.right);
                    heading = aim * (tilt * Vector3.forward);
                    Vector2 disc = Random.insideUnitCircle * Mathf.Max(0.02f, size * 0.2f);
                    offset = aim * new Vector3(disc.x, disc.y, 0f);
                }
                emit.position = position + offset;
                emit.velocity = heading * Random.Range(speed * 0.55f, speed);
                emit.startSize = Random.Range(size * 0.6f, size);
                emit.startLifetime = Random.Range(lifetime.x, lifetime.y);
                emit.rotation = Random.Range(0f, 360f);
                emit.startColor = tint;
                system.Emit(emit, 1);
            }
            return system;
        }

        /// <summary>Builds the shared systems now (say, when a fight loads) so the first burst of each kind doesn't hitch.</summary>
        public static void Prewarm()
        {
            for (int i = 0; i < Systems.Length; i++) SystemFor((Kind)i);
        }

        /// <summary>A fiery explosion: a fireball, sparks and a column of smoke, sized to the blast radius.</summary>
        public static void Explosion(Vector3 position, float radius)
        {
            float r = Mathf.Max(0.5f, radius);
            Spawn(Kind.Fireball, position, Vector3.zero, Mathf.RoundToInt(Mathf.Clamp(12f + r * 6f, 12f, 60f)), r * 0.9f, r * 2.2f);
            Spawn(Kind.Sparks, position, Vector3.up, Mathf.RoundToInt(Mathf.Clamp(16f + r * 6f, 16f, 70f)), 0.12f, 6f + r * 2.5f, 75f);
            Spawn(Kind.Smoke, position + Vector3.up * r * 0.3f, Vector3.up, Mathf.RoundToInt(Mathf.Clamp(6f + r * 3f, 6f, 30f)), r * 0.9f, 1.5f + r * 0.6f, 50f);
        }

        private static Vector2 Lifetime(Kind kind)
        {
            switch (kind)
            {
                case Kind.Sand: return new Vector2(0.6f, 1.3f);
                case Kind.Smoke: return new Vector2(1.2f, 2.2f);
                case Kind.Sparks: return new Vector2(0.25f, 0.6f);
                default: return new Vector2(0.35f, 0.7f);
            }
        }

        private static Color DefaultColor(Kind kind)
        {
            switch (kind)
            {
                case Kind.Sand: return new Color(0.86f, 0.74f, 0.52f, 0.85f);
                case Kind.Smoke: return new Color(0.18f, 0.17f, 0.16f, 0.7f);
                case Kind.Sparks: return new Color(1f, 0.8f, 0.4f, 1f);
                default: return Color.white;
            }
        }

        /// <summary>The shared system for a kind, made the first time it is needed (and again after a scene change).</summary>
        private static ParticleSystem SystemFor(Kind kind)
        {
            ParticleSystem existing = Systems[(int)kind];
            if (existing != null) return existing;

            var go = new GameObject(kind + " Bursts");
            go.hideFlags = HideFlags.DontSave;
            if (Application.isPlaying) Object.DontDestroyOnLoad(go);

            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = MaxParticlesPerKind;
            main.startSpeed = 0f;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.maxParticleSize = 3f;

            var sizeOverLife = system.sizeOverLifetime;
            var colorOverLife = system.colorOverLifetime;
            sizeOverLife.enabled = true;
            colorOverLife.enabled = true;
            switch (kind)
            {
                case Kind.Sand:
                    renderer.sharedMaterial = EffectMaterials.AlphaBlended(EffectMaterials.Look.Smoke);
                    main.gravityModifier = 0.6f;
                    sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.4f));
                    colorOverLife.color = Fade(0.05f, 0.45f);
                    Drag(system, 1.8f);
                    renderer.sortingOrder = 0;
                    break;
                case Kind.Smoke:
                    renderer.sharedMaterial = EffectMaterials.AlphaBlended(EffectMaterials.Look.Smoke);
                    main.gravityModifier = -0.08f;
                    sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2f));
                    colorOverLife.color = Fade(0.1f, 0.4f);
                    Drag(system, 2.5f);
                    renderer.sortingOrder = 0;
                    break;
                case Kind.Sparks:
                    renderer.sharedMaterial = EffectMaterials.Additive(EffectMaterials.Look.Glow);
                    renderer.renderMode = ParticleSystemRenderMode.Stretch;
                    renderer.velocityScale = 0.05f;
                    renderer.lengthScale = 1.2f;
                    main.gravityModifier = 1.2f;
                    sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
                    colorOverLife.color = Fade(0.01f, 0.5f);
                    renderer.sortingOrder = 4;
                    break;
                default: // Fireball
                    renderer.sharedMaterial = EffectMaterials.Additive(EffectMaterials.Look.Glow);
                    main.gravityModifier = -0.15f;
                    sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.25f, 1f), new Keyframe(1f, 1.3f)));
                    colorOverLife.color = FireballGradient();
                    Drag(system, 3f);
                    renderer.sortingOrder = 3;
                    break;
            }
            system.Play(false);
            Systems[(int)kind] = system;
            return system;
        }

        private static void Drag(ParticleSystem system, float drag)
        {
            var limit = system.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 100f;
            limit.drag = drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;
        }

        private static Gradient Fade(float fadeIn, float fadeOutFrom)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn),
                    new GradientAlphaKey(1f, Mathf.Max(fadeIn + 0.01f, fadeOutFrom)), new GradientAlphaKey(0f, 1f),
                });
            return gradient;
        }

        private static Gradient FireballGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.97f, 0.85f), 0f),
                    new GradientColorKey(new Color(1f, 0.6f, 0.15f), 0.3f),
                    new GradientColorKey(new Color(0.75f, 0.18f, 0.04f), 0.7f),
                    new GradientColorKey(new Color(0.25f, 0.06f, 0.02f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.4f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }
    }
}
