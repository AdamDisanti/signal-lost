using SignalLost.Combat;
using SignalLost.Effects;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// A wandering sand tornado (the Coreworm's sandstorm phase). It drifts around the arena, drags the player toward
    /// its middle, and grinds anything caught in the eye. Visual: a swirling column of sand particles.
    /// </summary>
    public sealed class SandCyclone : MonoBehaviour
    {
        private Vector3 centre;
        private float arenaRadius, pullRadius = 9f, eyeRadius = 2.2f, pull = 3.2f, damage = 6f, nextTick, speed = 2.6f;
        private Vector3 heading;
        private Transform player;
        private CharacterController playerBody;
        private Health playerHealth;
        private ParticleSystem swirl;
        private float fade = 1f;
        private bool dying;

        public static SandCyclone Spawn(Vector3 position, Vector3 arenaCentre, float arenaRadius, Transform player)
        {
            var go = new GameObject("Sand Cyclone");
            go.transform.position = position;
            var cyclone = go.AddComponent<SandCyclone>();
            cyclone.centre = arenaCentre;
            cyclone.arenaRadius = arenaRadius;
            cyclone.player = player;
            if (player != null)
            {
                cyclone.playerBody = player.GetComponentInParent<CharacterController>();
                cyclone.playerHealth = player.GetComponentInParent<Health>();
            }
            cyclone.heading = Random.insideUnitSphere;
            cyclone.heading.y = 0f;
            cyclone.BuildSwirl();
            return cyclone;
        }

        /// <summary>Winds down and removes itself.</summary>
        public void Dissipate()
        {
            dying = true;
            if (swirl != null) swirl.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject, 2.5f);
        }

        private void BuildSwirl()
        {
            swirl = gameObject.AddComponent<ParticleSystem>();
            swirl.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = swirl.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.82f, 0.68f, 0.46f, 0.55f);
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = swirl.emission;
            emission.rateOverTime = 160f;
            var shape = swirl.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 1.2f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var velocity = swirl.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(4f, 6f);
            velocity.radial = new ParticleSystem.MinMaxCurve(0.4f);
            var size = swirl.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
            var color = swirl.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = EffectMaterials.AlphaBlended(EffectMaterials.Look.Smoke);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 3f;
            swirl.Play();
        }

        private void Update()
        {
            // Wander, turning back toward the middle near the edge.
            heading = Vector3.RotateTowards(heading, Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.7f + GetHashCode()) * 90f, 0f) * heading, 0.6f * Time.deltaTime, 0f);
            Vector3 fromCentre = transform.position - centre;
            fromCentre.y = 0f;
            if (fromCentre.magnitude > arenaRadius * 0.75f) heading = Vector3.Lerp(heading, -fromCentre.normalized, 2f * Time.deltaTime);
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.01f) heading = Vector3.forward;
            heading.Normalize();
            transform.position += heading * speed * Time.deltaTime;

            if (dying || player == null || playerBody == null || !playerBody.enabled) return;
            Vector3 toEye = transform.position - player.position;
            toEye.y = 0f;
            float distance = toEye.magnitude;
            if (distance > pullRadius) return;
            float strength = pull * (1f - distance / pullRadius) * fade;
            if (distance > 0.3f) playerBody.Move(toEye.normalized * strength * Time.deltaTime);
            if (distance <= eyeRadius && Time.time >= nextTick && playerHealth != null && !playerHealth.IsDead)
            {
                nextTick = Time.time + 0.5f;
                playerHealth.TakeDamage(damage, gameObject);
                MiniBossHud.SandBlind(0.8f);
            }
        }
    }
}
