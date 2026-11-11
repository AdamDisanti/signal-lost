using SignalLost.Combat;
using SignalLost.Inventory;
using SignalLost.Scanner;
using SignalLost.UI;
using UnityEngine;

namespace SignalLost.Player
{
    /// <summary>
    /// Applies the upgrades in the player's pockets (<see cref="UpgradeCatalog"/>) to the gear: laser damage and
    /// cooldowns, finisher, scanner range, drinks before a blackout, max health, slide distance, the fists' knuckles and
    /// the Moon Shoes' run speed, jump height and double jump. Re-applies after
    /// every purchase, from the base tuning, so nothing stacks twice. Also uses supplies, and shows an
    /// "upgrade installed" card for each new upgrade.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class PlayerUpgrades : MonoBehaviour
    {
        private PlayerInventory inventory;
        private MiningLaser laser;
        private ScannerController scanner;
        private PlayerFlask flask;
        private Health health;
        private FirstPersonController movement;
        private float baseMaxHealth;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            laser = GetComponent<MiningLaser>();
            scanner = GetComponentInChildren<ScannerController>(true);
            flask = GetComponent<PlayerFlask>();
            health = GetComponent<Health>();
            movement = GetComponent<FirstPersonController>();
            baseMaxHealth = health != null ? health.Max : 100f;
        }

        private void OnEnable()
        {
            inventory.Purchased += OnPurchased;
            Apply();
        }

        private void OnDisable() => inventory.Purchased -= OnPurchased;

        private void OnPurchased(Upgrade upgrade)
        {
            if (!upgrade.OneOff) return;
            Apply();
            GameplayOverlay.Instance.ShowItemCard(upgrade.Name, upgrade.Description + "\nSee it on your gear in the pockets menu (Tab).",
                UiTheme.Accent, "UPGRADE INSTALLED");
        }

        /// <summary>
        /// Testing tool (the melee arena): only knuckle and Moon Shoe tiers up to these count, so each tier can be tried
        /// without changing what is owned. Call <see cref="Apply"/> after changing them.
        /// </summary>
        public int KnuckleTierLimit { get; set; } = 3;
        public int ShoeTierLimit { get; set; } = 3;

        /// <summary>Sets every stat from the base tuning plus everything owned.</summary>
        public void Apply()
        {
            float boltDamage = 1f, boltCooldown = 1f, finisherDamage = 1f, finisherStun = 0f, range = 0f, maxHealth = 0f, slide = 1f;
            float melee = 1f, fistSpeed = 1f, run = 1f, jump = 1f;
            int drinks = 0, knuckles = 0;
            bool doubleJump = false;
            foreach (string id in inventory.Upgrades)
            {
                Upgrade upgrade = UpgradeCatalog.Find(id);
                if (upgrade == null) continue;
                if ((upgrade.Line == "gaussian_knuckles" && upgrade.Tier > KnuckleTierLimit) || (upgrade.Line == "moon_shoes" && upgrade.Tier > ShoeTierLimit)) continue;
                boltDamage += upgrade.BoltDamage;
                boltCooldown += upgrade.BoltCooldown;
                finisherDamage += upgrade.FinisherDamage;
                finisherStun += upgrade.FinisherStun;
                range += upgrade.ScannerRange;
                maxHealth += upgrade.MaxHealth;
                slide += upgrade.SlideDistance;
                drinks += upgrade.ExtraDrinks;
                melee += upgrade.MeleeDamage;
                fistSpeed += upgrade.FistSpeed;
                run += upgrade.RunSpeed;
                jump += upgrade.JumpHeight;
                knuckles = Mathf.Max(knuckles, upgrade.KnuckleTier);
                doubleJump |= upgrade.DoubleJump;
            }
            if (laser != null)
            {
                laser.BoltDamageMultiplier = boltDamage;
                laser.BoltCooldownMultiplier = Mathf.Max(0.1f, boltCooldown);
                laser.FinisherDamageMultiplier = finisherDamage;
                laser.FinisherStunBonus = finisherStun;
            }
            if (scanner != null) scanner.RangeBonus = range;
            if (flask != null) flask.BlackoutBonus = drinks;
            if (movement != null)
            {
                movement.SlideDistanceScale = slide;
                movement.SetSpeedBonus(this, run);
                movement.JumpHeightScale = jump;
                movement.DoubleJump = doubleJump;
            }
            var fists = GetComponent<SignalLost.Melee.PlayerFists>(); // added at runtime by PlayerGear
            if (fists != null)
            {
                fists.KnuckleTier = knuckles;
                fists.DamageMultiplier = melee;
                fists.SpeedBonus = fistSpeed;
            }
            if (health != null && !Mathf.Approximately(health.Max, baseMaxHealth + maxHealth)) health.SetMax(baseMaxHealth + maxHealth);
        }

        /// <summary>Uses one of a supply (pills, snacks). Returns false if there is none or it would do nothing.</summary>
        public bool UseSupply(string id)
        {
            Upgrade supply = UpgradeCatalog.Find(id);
            if (supply == null || !supply.IsSupply || inventory.SupplyCount(id) <= 0) return false;
            bool helps = (supply.SoberUp > 0 && flask != null && flask.DrunkStacks > 0) ||
                         (supply.Heal > 0f && health != null && health.Current < health.Max);
            if (!helps || !inventory.TakeSupply(id)) return false;
            if (supply.SoberUp > 0 && flask != null) flask.SoberBy(supply.SoberUp);
            if (supply.Heal > 0f && health != null) health.Heal(supply.Heal);
            return true;
        }
    }
}
