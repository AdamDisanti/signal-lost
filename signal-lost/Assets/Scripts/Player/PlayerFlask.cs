using System;
using SignalLost.Combat;
using SignalLost.Inventory;
using SignalLost.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SignalLost.Player
{
    /// <summary>
    /// The Scotch flask (F): a quick sip with the left hand while the right hand keeps its tool. Each sip heals half
    /// as much as the last while the player is still drunk; one drink wears off every <see cref="secondsPerStack"/>,
    /// and drinking past <see cref="BlackoutAfterStacks"/> knocks them out (<see cref="PlayerLife.Blackout"/>).
    /// Drunkenness slows and sways movement (<see cref="FirstPersonController.Impairment"/>) and blurs the view
    /// through the "Drunk Vision" volume. The left-hand animation (<see cref="FirstPersonHands"/>) reads
    /// <see cref="DrinkProgress"/>.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(PlayerGear))]
    public sealed class PlayerFlask : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [Tooltip("Post-processing volume with Lens Distortion and Chromatic Aberration for the drunk blur.")]
        [SerializeField] private Volume drunkVolume;

        [Header("Tuning (placeholders)")]
        [Tooltip("Health from each sip, by how many drinks are already in you (10-23 curve: gentler than halving). " +
                 "The last value repeats for any further sips.")]
        [SerializeField] private float[] healPerSip = { 35f, 28f, 22f, 17f, 13f, 10f };
        [Tooltip("Drunk stacks allowed; the next sip after this many causes a blackout.")]
        [SerializeField, Min(1)] private int blackoutAfterStacks = 5;
        [Tooltip("Length of the sip animation. Healing lands halfway through.")]
        [SerializeField, Min(0.1f)] private float drinkSeconds = 1f;
        [Tooltip("Seconds after a sip before the next one.")]
        [SerializeField, Min(0f)] private float drinkCooldown = 1f;
        [Tooltip("Seconds for one drink to wear off (after the last sip).")]
        [SerializeField, Min(0.1f)] private float secondsPerStack = 15f;

        private Health health;
        private PlayerGear gear;
        private PlayerLife life;
        private FirstPersonController movement;
        private InputActionAsset runtimeActions;
        private InputAction drink;
        private LensDistortion distortion;
        private ChromaticAberration aberration;
        private float drinkStarted = float.NegativeInfinity, nextDrink, nextSober;
        private bool sipApplied;

        public int DrunkStacks { get; private set; }
        public int BlackoutAfterStacks => BlackoutLimit;
        public float HealWhenSober => HealFor(0);
        /// <summary>Health the next sip restores: full while sober, halved for every drunk stack.</summary>
        public float NextSipHeal => HealFor(DrunkStacks);

        /// <summary>
        /// How far the current drink has worn off: 1 just after a sip, falling to 0 as the next stack wears off.
        /// The HUD drains the top drunk pip with it so the player can see when it is safe to drink again.
        /// </summary>
        public float StackWearing => DrunkStacks > 0 ? Mathf.Clamp01((nextSober - Time.time) / secondsPerStack) : 0f;

        /// <summary>Upgrades (Cast-Iron Liver) raise how many drinks fit before a blackout.</summary>
        public int BlackoutBonus { get; set; }

        private int BlackoutLimit => blackoutAfterStacks + BlackoutBonus;

        private float HealFor(int stacks) =>
            healPerSip == null || healPerSip.Length == 0 ? 35f : healPerSip[Mathf.Clamp(stacks, 0, healPerSip.Length - 1)];
        public bool IsDrinking { get; private set; }
        /// <summary>0 to 1 through the sip animation; 0 when not drinking.</summary>
        public float DrinkProgress => IsDrinking ? Mathf.Clamp01((Time.time - drinkStarted) / drinkSeconds) : 0f;
        /// <summary>0 (sober) to 1 (one sip from blacking out).</summary>
        public float Drunkenness => Mathf.Clamp01(DrunkStacks / (float)BlackoutLimit);
        /// <summary>Extra blur (0 to 1) from cutscenes such as waking up; visual only, never slows movement.</summary>
        public float ExtraDaze { get; set; }

        /// <summary>Raised when a sip lands. Argument: health restored.</summary>
        public event Action<float> Sipped;

        private void Awake()
        {
            health = GetComponent<Health>();
            gear = GetComponent<PlayerGear>();
            life = GetComponent<PlayerLife>();
            movement = GetComponent<FirstPersonController>();
            if (inputActions != null)
            {
                runtimeActions = Instantiate(inputActions);
                InputActionMap map = runtimeActions.FindActionMap("Player", true);
                map.bindingMask = InputBinding.MaskByGroups("Keyboard&Mouse", "Gamepad");
                drink = map.FindAction("Drink", true);
            }
            if (drunkVolume != null)
            {
                VolumeProfile profile = drunkVolume.profile; // a private copy, so the asset is never changed
                profile.TryGet(out distortion);
                profile.TryGet(out aberration);
                drunkVolume.weight = 0f;
            }
        }

        private void OnEnable()
        {
            drink?.actionMap.Enable();
            if (life != null) life.Respawned += SoberUp;
        }

        private void OnDisable()
        {
            drink?.actionMap.Disable();
            if (life != null) life.Respawned -= SoberUp;
        }

        private void OnDestroy()
        {
            if (runtimeActions != null) Destroy(runtimeActions);
        }

        /// <summary>Takes a sip if the player has the flask and nothing stops them. Returns true if they started drinking.</summary>
        public bool TryDrink()
        {
            if (!gear.Has(PlayerItem.Flask) || IsDrinking || Time.time < nextDrink || health.IsDead ||
                (life != null && life.IsRespawning) || UiMenus.AnyOpen) return false;
            IsDrinking = true;
            sipApplied = false;
            drinkStarted = Time.time;
            nextDrink = Time.time + drinkSeconds + drinkCooldown;
            return true;
        }

        private void Update()
        {
            bool controllable = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
            if (controllable && drink != null && drink.WasPressedThisFrame()) TryDrink();

            if (IsDrinking)
            {
                if (!sipApplied && DrinkProgress >= 0.6f) // as the flask reaches the mouth
                {
                    sipApplied = true;
                    float healed = NextSipHeal;
                    health.Heal(healed);
                    DrunkStacks++;
                    nextSober = Time.time + secondsPerStack;
                    Sipped?.Invoke(healed);
                    if (DrunkStacks > BlackoutLimit && life != null) life.Blackout(PlayerLife.BlackoutMessage);
                }
                if (DrinkProgress >= 1f) IsDrinking = false;
            }

            if (DrunkStacks > 0 && Time.time >= nextSober && (life == null || !life.IsRespawning))
            {
                DrunkStacks--;
                nextSober = Time.time + secondsPerStack;
            }
            ApplyIntoxication();
        }

        /// <summary>Wears off some drinks at once (Hangover Pills).</summary>
        public void SoberBy(int drinks)
        {
            DrunkStacks = Mathf.Max(0, DrunkStacks - drinks);
            nextSober = Time.time + secondsPerStack;
        }

        private void SoberUp()
        {
            DrunkStacks = 0;
            IsDrinking = false;
            nextDrink = Time.time;
        }

        private void ApplyIntoxication()
        {
            if (movement != null) movement.Impairment = Drunkenness;
            if (drunkVolume == null) return;
            drunkVolume.weight = Mathf.Max(Drunkenness, ExtraDaze);
            if (distortion != null) distortion.intensity.value = -0.4f - Mathf.Sin(Time.time * 1.4f) * 0.16f;
            if (aberration != null) aberration.intensity.value = 1f;
        }
    }
}
