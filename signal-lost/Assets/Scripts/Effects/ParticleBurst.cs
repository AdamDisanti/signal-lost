using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Effects
{
    /// <summary>
    /// One-shot particle bursts built in code, for hits and attacks: a spray of sand, a puff of smoke, a shower of
    /// sparks, a fireball. Each burst is its own short-lived GameObject that removes itself when its particles die.
    /// Uses the same textures and materials as <see cref="FireEffect"/>.
    /// </summary>
    public static class ParticleBurst
    {
        public enum Kind { Sand, Smoke, Sparks, Fireball }

        /// <param name="direction">Which way the burst flies (any length). Zero: in every direction.</param>
        /// <param name="size">Rough size of each puff in metres.</param>
        /// <param name="speed">Launch speed in metres per second.</param>
        /// <param name="spread">Half-angle of the cone the particles fly out in, in degrees.</param>
        public static ParticleSystem Spawn(Kind kind, Vector3 position, Vector3 direction, int count, float size, float speed,
            float spread = 25f, Color? color = null)
        {
            var go = new GameObject(kind + " Burst");
            go.transform.position = position;
            bool everywhere = direction.sqrMagnitude < 0.0001f;
            go.transform.rotation = everywhere ? Quaternion.identity : Quaternion.LookRotation(direction.normalized);

            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.1f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(1, count);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.55f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = system.emission;
            emission.enabled = false;

            var shape = system.shape;
            shape.enabled = true;
            if (everywhere)
            {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = Mathf.Max(0.05f, size * 0.25f);
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = Mathf.Clamp(spread, 0f, 89f);
                shape.radius = Mathf.Max(0.02f, size * 0.2f);
            }

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
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
                    main.startColor = color ?? new Color(0.86f, 0.74f, 0.52f, 0.85f);
                    main.gravityModifier = 0.6f;
                    sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.4f));
                    colorOverLife.color = Fade(0.05f, 0.45f);
                    Drag(system, 1.8f);
                    renderer.sortingOrder = 0;
                    break;
                case Kind.Smoke:
                    renderer.sharedMaterial = EffectMaterials.AlphaBlended(EffectMaterials.Look.Smoke);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
                    main.startColor = color ?? new Color(0.18f, 0.17f, 0.16f, 0.7f);
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
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
                    main.startColor = color ?? new Color(1f, 0.8f, 0.4f, 1f);
                    main.gravityModifier = 1.2f;
                    sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
                    colorOverLife.color = Fade(0.01f, 0.5f);
                    renderer.sortingOrder = 4;
                    break;
                default: // Fireball
                    renderer.sharedMaterial = EffectMaterials.Additive(EffectMaterials.Look.Glow);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                    main.startColor = color ?? Color.white;
                    main.gravityModifier = -0.15f;
                    sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.25f, 1f), new Keyframe(1f, 1.3f)));
                    colorOverLife.color = FireballGradient();
                    Drag(system, 3f);
                    renderer.sortingOrder = 3;
                    break;
            }

            system.Play(false);
            system.Emit(Mathf.Max(1, count));
            return system;
        }

        /// <summary>A fiery explosion: a fireball, sparks and a column of smoke, sized to the blast radius.</summary>
        public static void Explosion(Vector3 position, float radius)
        {
            float r = Mathf.Max(0.5f, radius);
            Spawn(Kind.Fireball, position, Vector3.zero, Mathf.RoundToInt(Mathf.Clamp(12f + r * 6f, 12f, 60f)), r * 0.9f, r * 2.2f);
            Spawn(Kind.Sparks, position, Vector3.up, Mathf.RoundToInt(Mathf.Clamp(16f + r * 6f, 16f, 70f)), 0.12f, 6f + r * 2.5f, 75f);
            Spawn(Kind.Smoke, position + Vector3.up * r * 0.3f, Vector3.up, Mathf.RoundToInt(Mathf.Clamp(6f + r * 3f, 6f, 30f)), r * 0.9f, 1.5f + r * 0.6f, 50f);
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
