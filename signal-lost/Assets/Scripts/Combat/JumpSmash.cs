using SignalLost.Audio;
using SignalLost.Effects;
using SignalLost.Player;
using UnityEngine;

namespace SignalLost.Combat
{
    /// <summary>
    /// The jump smash (Lawson, 11-07), the player's take on the Chief Toad's jump slam: in mid-air with the mining laser
    /// out, press crouch (C) to raise the laser overhead and dive straight down. Landing slams a shockwave into the
    /// ground that hurts and shoves everything around it. The higher above the landing spot it started, the harder and
    /// wider it hits, up to a cap (<see cref="FullPowerHeight"/>), so a smash off a rock or a hill beats one from a
    /// plain jump. The Heavy Slam upgrade adds to it like it does to the combo finisher.
    ///
    /// Added by <see cref="MiningLaser"/> to the player at runtime. Numbers are provisional (not agreed balance).
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public sealed class JumpSmash : MonoBehaviour
    {
        [Tooltip("Must be at least this high above the ground to start a smash (a plain jump reaches about 1.2 m).")]
        [SerializeField, Min(0.2f)] private float minHeight = 0.8f;
        [Tooltip("A smash from this high or higher hits with full power.")]
        [SerializeField, Min(1f)] private float fullPowerHeight = 8f;
        [SerializeField, Min(5f)] private float diveSpeed = 24f;
        [SerializeField, Min(0f)] private float minDamage = 15f;
        [SerializeField, Min(0f)] private float maxDamage = 60f;
        [SerializeField, Min(0.5f)] private float minRadius = 2.5f;
        [SerializeField, Min(0.5f)] private float maxRadius = 5.5f;
        [Tooltip("Share of the damage dealt at the edge of the shockwave.")]
        [SerializeField, Range(0f, 1f)] private float edgeDamage = 0.5f;
        [SerializeField, Min(0f)] private float minKnockback = 1f;
        [SerializeField, Min(0f)] private float maxKnockback = 3f;
        [Tooltip("Seconds after landing before the next smash.")]
        [SerializeField, Min(0f)] private float cooldown = 1.2f;

        private FirstPersonController controller;
        private PlayerGear gear;
        private MiningLaser laser;
        private float startY, readyAt, diveStarted;
        private Material ringMaterial;
        private System.Func<bool> hook;

        public bool IsDiving { get; private set; }
        public float FullPowerHeight => fullPowerHeight;
        public float MinHeight => minHeight;
        /// <summary>Raised on landing with the drop height, its power (0-1), and how many things it hurt.</summary>
        public event System.Action<float, float, int> Landed;

        /// <summary>Damage at the centre for a smash from <paramref name="height"/> metres (before upgrades).</summary>
        public float DamageFor(float height) => Mathf.Lerp(minDamage, maxDamage, Power(height));
        public float RadiusFor(float height) => Mathf.Lerp(minRadius, maxRadius, Power(height));
        private float Power(float height) => Mathf.Clamp01(Mathf.InverseLerp(minHeight, fullPowerHeight, height));

        private void Awake()
        {
            controller = GetComponent<FirstPersonController>();
            gear = GetComponent<PlayerGear>();
            laser = GetComponent<MiningLaser>();
            hook = TryStart;
        }

        private void OnEnable() => controller.AirCrouchPressed = hook;

        private void OnDisable()
        {
            if (controller.AirCrouchPressed == hook) controller.AirCrouchPressed = null;
            Cancel();
        }

        /// <summary>Starts a smash if one is allowed now. Returns true if it started. (Also for tests.)</summary>
        public bool TryStart()
        {
            if (IsDiving || Time.time < readyAt || controller.IsGrounded || controller.IsStunned) return false;
            if (laser == null || !laser.isActiveAndEnabled || laser.IsSwinging) return false;
            if (gear != null && gear.InHand != PlayerGear.Held.Laser) return false;
            if (HeightAboveGround() < minHeight) return false;

            IsDiving = true;
            diveStarted = Time.time;
            startY = transform.position.y;
            laser.SetSmashRaised(true);
            controller.Dive(diveSpeed);
            Sfx.PlayAt("laser_charge", transform.position, 0.5f, 1.5f, 15f);
            return true;
        }

        private void Update()
        {
            if (!IsDiving) return;
            if (laser == null || !laser.isActiveAndEnabled || controller.IsStunned) { Cancel(); return; }
            if (!controller.IsGrounded)
            {
                controller.Dive(diveSpeed); // keep driving down (gravity only ever adds to it)
                if (Time.time - diveStarted > 6f) Cancel(); // fell off the world
                return;
            }
            Land();
        }

        private void Cancel()
        {
            if (!IsDiving) return;
            IsDiving = false;
            if (laser != null) laser.SetSmashRaised(false);
        }

        private void Land()
        {
            IsDiving = false;
            readyAt = Time.time + cooldown;
            Vector3 feet = transform.position;
            float height = Mathf.Max(0f, startY - feet.y);
            float power = Power(height);
            float bonus = laser != null ? laser.FinisherDamageMultiplier : 1f; // Heavy Slam
            float damage = DamageFor(height) * bonus;
            float radius = RadiusFor(height);
            float knockback = Mathf.Lerp(minKnockback, maxKnockback, power);
            int hurt = AreaBlast.Explode(feet + Vector3.up * 0.4f, radius, damage, edgeDamage, knockback, gameObject, null, null);
            if (laser != null) laser.SmashLanded(power);
            Effects(feet, radius, power);
            Landed?.Invoke(height, power, hurt);
        }

        // A shockwave across the ground in the laser's colour, dust and sparks thrown out, a slam and a shake.
        private void Effects(Vector3 feet, float radius, float power)
        {
            Color energy = laser != null ? laser.EnergyColor : new Color(0.4f, 0.9f, 1f);
            if (ringMaterial == null) ringMaterial = EffectMaterials.Additive(EffectMaterials.Look.Beam);
            Vector3 ground = feet + Vector3.up * 0.12f;
            Ring(ground, radius, 0.45f, Mathf.Lerp(0.5f, 1f, power), new Color(energy.r, energy.g, energy.b, 0.9f));
            Ring(ground, radius * 0.6f, 0.3f, Mathf.Lerp(0.3f, 0.6f, power), new Color(1f, 1f, 1f, 0.8f));
            int dust = Mathf.RoundToInt(Mathf.Lerp(10f, 26f, power));
            for (int i = 0; i < 8; i++)
            {
                Vector3 out8 = Quaternion.Euler(0f, i * 45f + Random.Range(-15f, 15f), 0f) * Vector3.forward;
                ParticleBurst.Spawn(ParticleBurst.Kind.Sand, feet + out8 * 0.6f, out8 + Vector3.up * 0.35f, dust / 8 + 1,
                    Mathf.Lerp(0.35f, 0.6f, power), Mathf.Lerp(3f, 7f, power), 25f, new Color(0.55f, 0.5f, 0.42f, 0.7f));
            }
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, feet + Vector3.up * 0.2f, Vector3.up, Mathf.RoundToInt(Mathf.Lerp(14f, 34f, power)),
                0.07f, Mathf.Lerp(5f, 9f, power), 80f, Color.Lerp(energy, Color.white, 0.35f));
            ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, feet + Vector3.up * 0.3f, Vector3.zero, 5, Mathf.Lerp(0.5f, 1f, power), 0.6f, 90f,
                new Color(energy.r, energy.g, energy.b, 0.5f));
            CameraShake.Add(Mathf.Lerp(0.3f, 0.75f, power));
            Sfx.PlayAt("laser_slam", feet, Mathf.Lerp(0.75f, 1f, power), Mathf.Lerp(1.1f, 0.7f, power), 35f);
            Sfx.PlayAt("player_land", feet, 0.9f, Mathf.Lerp(0.9f, 0.6f, power), 20f);
        }

        private void Ring(Vector3 at, float radius, float lifetime, float width, Color color)
        {
            ExpandingRing ring = ExpandingRing.Spawn(ringMaterial, at, radius, lifetime, width);
            var line = ring.GetComponent<LineRenderer>();
            line.startColor = line.endColor = color;
        }

        private float HeightAboveGround()
        {
            float best = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(transform.position + Vector3.up * 0.1f, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                best = Mathf.Min(best, hit.distance - 0.1f);
            }
            return best;
        }
    }
}
