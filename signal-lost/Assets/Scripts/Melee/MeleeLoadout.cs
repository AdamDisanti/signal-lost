using SignalLost.Player;
using UnityEngine;

namespace SignalLost.Melee
{
    /// <summary>
    /// Switches the melee update on for one player (docs/melee-rage.md). It is kept to the final boss level and the melee
    /// test arena (Adam, 11-10: so it doesn't touch the rest of the game while the team works on it); both add this through
    /// their playtest loadout (<see cref="SignalLost.Finale.FinaleLoadout"/>).
    /// It adds the fists to the Q cycle (<see cref="PlayerGear.AddFists"/>), the rage meter and the double jump's
    /// effect, and gives the Gaussian Brass Knuckles and Moon Shoes at the tiers set here. They aren't sold in any shop yet,
    /// so the tiers' numbers live here instead of in the upgrade catalog. The arena's testing keys change the tiers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MeleeLoadout : MonoBehaviour
    {
        // Per tier (0 = none): punch damage and run speed with the fists up (knuckles), run speed and jump height (shoes).
        private static readonly float[] KnuckleDamage = { 1f, 1.35f, 1.55f, 1.75f };
        private static readonly float[] KnuckleSpeed = { 1f, 1.08f, 1.12f, 1.16f };
        private static readonly float[] ShoeSpeed = { 1f, 1.08f, 1.16f, 1.22f };
        private static readonly float[] ShoeJump = { 1f, 1.2f, 1.4f, 1.55f };

        [SerializeField, Range(0, 3)] private int knuckleTier = 3;
        [SerializeField, Range(0, 3)] private int shoeTier = 3;

        private PlayerFists fists;
        private FirstPersonController movement;

        public static string Numeral(int tier) => tier switch { 1 => "I", 2 => "II", 3 => "III", _ => "" };

        /// <summary>
        /// Gaussian Brass Knuckles tier (0 bare fists): 1 the magnet lunge; 2 overcharge marks, chain lightning and the
        /// magnet pull; 3 finishers. Each tier also hits harder and runs faster with the fists up.
        /// </summary>
        public int KnuckleTier
        {
            get => knuckleTier;
            set { knuckleTier = Mathf.Clamp(value, 0, 3); Apply(); }
        }

        /// <summary>Moon Shoes tier (0 none): run speed and jump height; 3 adds the directional double jump.</summary>
        public int ShoeTier
        {
            get => shoeTier;
            set { shoeTier = Mathf.Clamp(value, 0, 3); Apply(); }
        }

        private void Awake()
        {
            movement = GetComponent<FirstPersonController>();
            var gear = GetComponent<PlayerGear>();
            if (gear == null || gear.Laser == null || gear.Laser.InputActions == null || GetComponent<PlayerLife>() == null)
            {
                Debug.LogWarning("MeleeLoadout needs the full player (gear, mining laser, life); turning off.", this);
                enabled = false;
                return;
            }
            fists = GetComponent<PlayerFists>();
            if (fists == null) fists = gameObject.AddComponent<PlayerFists>();
            if (GetComponent<RageMeter>() == null) gameObject.AddComponent<RageMeter>();
            if (movement != null && GetComponent<MoonShoesFx>() == null) gameObject.AddComponent<MoonShoesFx>();
            gear.AddFists(fists);
            Apply();
        }

        private void Apply()
        {
            if (fists != null)
            {
                fists.KnuckleTier = knuckleTier;
                fists.DamageMultiplier = KnuckleDamage[knuckleTier];
                fists.SpeedBonus = KnuckleSpeed[knuckleTier];
            }
            if (movement != null)
            {
                movement.SetSpeedBonus(this, ShoeSpeed[shoeTier]);
                movement.JumpHeightScale = ShoeJump[shoeTier];
                movement.DoubleJump = shoeTier >= 3;
            }
        }
    }
}
