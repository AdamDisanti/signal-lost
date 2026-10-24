using System;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Inventory;
using SignalLost.Scanner;
using SignalLost.UI;
using UnityEngine;

namespace SignalLost.Player
{
    /// <summary>
    /// The player's three items and what is in their right hand (meeting notes 10-22): the mining laser OR the
    /// scanner, swapped with Q (the scanner's own toggle). The flask is drunk with the left hand (<see cref="PlayerFlask"/>).
    /// Other systems put the tools away while they need the hands (drinking, respawning) with <see cref="Holster"/>.
    /// Items not owned yet stay unusable; collecting one shows its "new gear" card.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class PlayerGear : MonoBehaviour
    {
        public enum Held { Nothing, Laser, Scanner }

        [SerializeField] private MiningLaser laser;
        [Tooltip("The ScannerRig's toggle under the camera. Q only works once the scanner is owned.")]
        [SerializeField] private ScannerToggle scanner;
        [Tooltip("Items the player already has when the scene starts (free-play scenes start with everything).")]
        [SerializeField] private PlayerItem[] startingItems = Array.Empty<PlayerItem>();
        [Tooltip("Show a card explaining each item the first time it is collected.")]
        [SerializeField] private bool introduceItems = true;

        private PlayerInventory inventory;
        private readonly HashSet<object> holsters = new HashSet<object>();
        private bool scannerWasRaised;

        public PlayerInventory Inventory => inventory;
        public MiningLaser Laser => laser;
        public ScannerToggle Scanner => scanner;
        public bool ScannerRaised => scanner != null && scanner.isActiveAndEnabled && scanner.IsRaised;
        public bool Holstered => holsters.Count > 0;

        /// <summary>What the right hand is holding right now.</summary>
        public Held InHand => Holstered ? Held.Nothing : ScannerRaised ? Held.Scanner : Has(PlayerItem.MiningLaser) ? Held.Laser : Held.Nothing;

        /// <summary>Raised after an item is collected (not for starting items).</summary>
        public event Action<PlayerItem> Collected;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            if (laser == null) laser = GetComponent<MiningLaser>();
            if (scanner == null) scanner = GetComponentInChildren<ScannerToggle>(true);
            foreach (PlayerItem item in startingItems) inventory.Add(item);
            Refresh();
        }

        public bool Has(PlayerItem item) => inventory.Has(item);

        /// <summary>Gives the player an item. Returns false if they already had it.</summary>
        public bool Collect(PlayerItem item)
        {
            if (!inventory.Add(item)) return false;
            Refresh();
            if (introduceItems) Introduce(item);
            Collected?.Invoke(item);
            return true;
        }

        /// <summary>
        /// Brings the hands up to date with the pockets after items were put there directly (loading a save), without
        /// the "new gear" cards.
        /// </summary>
        public void Sync() => Refresh();

        /// <summary>Puts both tools away until <see cref="Unholster"/> is called with the same key.</summary>
        public void Holster(object key)
        {
            if (holsters.Add(key)) Refresh();
        }

        public void Unholster(object key)
        {
            if (holsters.Remove(key)) Refresh();
        }

        // Q is read by the ScannerToggle itself; this only follows it, after it has handled this frame's press.
        private void LateUpdate()
        {
            if (ScannerRaised == scannerWasRaised) return;
            scannerWasRaised = ScannerRaised;
            Refresh();
        }

        private void Refresh()
        {
            if (scanner != null)
            {
                bool canScan = Has(PlayerItem.Scanner) && !Holstered;
                if (!canScan && scanner.enabled) scanner.enabled = false; // disabling also lowers it
                else if (canScan && !scanner.enabled) scanner.enabled = true;
            }
            if (laser != null) laser.enabled = Has(PlayerItem.MiningLaser) && !Holstered && !ScannerRaised;
            scannerWasRaised = ScannerRaised;
        }

        // A short card explaining what the new gear does and how to use it.
        private void Introduce(PlayerItem item)
        {
            GameplayOverlay overlay = GameplayOverlay.Instance;
            var flask = GetComponent<PlayerFlask>();
            if (item == PlayerItem.MiningLaser && laser != null)
            {
                string combo = laser.ComboCooldown > 0f ? $" The third hit stuns critters, then needs {laser.ComboCooldown:0.#} s to recharge." : "";
                overlay.ShowItemCard("Mining Laser",
                    "Your trusty, slightly rusty multitool.\n" +
                    $"<b>Left click</b>  three-hit combo.{combo}\n" +
                    $"<b>Hold right click</b>  charge a bolt, release to fire ({laser.TapShotCooldown:0.#}-{laser.ShotCooldown:0.#} s cooldown). " +
                    "A full charge bursts on impact.",
                    UiTheme.Accent);
            }
            else if (item == PlayerItem.Scanner)
            {
                overlay.ShowItemCard("Scanner",
                    "Sniffs out ship junk, critters and anyone with a job for you.\n" +
                    "<b>Q</b>  swap between the scanner and the laser.\n" +
                    "It locks onto your current objective; the bar fills and the beeps quicken as you get close.",
                    UiTheme.SpeakerColor("Cavendish"));
            }
            else if (item == PlayerItem.Flask)
            {
                string heal = flask != null ? $"+{flask.HealWhenSober:0} HP while sober, half as much for every drink still in your system." : "heals you.";
                string limit = flask != null ? $"More than {flask.BlackoutAfterStacks} sips in a row and you black out." : "";
                overlay.ShowItemCard("Scotch Flask",
                    "Kirkland Signature. Medicinal. Probably.\n" +
                    $"<b>F</b>  take a quick sip: {heal}\n{limit}",
                    new Color(1f, 0.78f, 0.34f));
            }
        }
    }
}
