using System;
using System.Collections;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.Inventory;
using SignalLost.NPC;
using SignalLost.Scanner;
using SignalLost.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SignalLost.Player
{
    [RequireComponent(typeof(Health), typeof(FirstPersonController), typeof(PlayerInventory))]
    public sealed class TutorialPlayer : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Camera view;
        [SerializeField] private MiningLaser laser;
        [SerializeField] private CombatDebugHud combatHud;
        [SerializeField] private Transform heldFlask;
        [SerializeField] private GameObject enemies;
        [SerializeField] private Volume drunkVolume;
        [SerializeField] private HUDController hud;
        [SerializeField] private TextMeshProUGUI healthText, promptText, drunkText, messageText;
        [SerializeField] private Image blackoutOverlay;
        [SerializeField] private TutorialQuestFlow quest;
        [Tooltip("The ScannerRig's toggle under the player camera. Q only works once the scanner is collected.")]
        [SerializeField] private ScannerToggle scanner;

        [Header("Prototype tuning")]
        [SerializeField, Min(0.5f)] private float pickupDistance = 3f;
        [Tooltip("Health from a sip while sober. Each drunk stack halves it until the player sobers up.")]
        [SerializeField, Min(1f)] private float healAmount = 35f;
        [Tooltip("Drunk stacks allowed; the next sip after this many causes a blackout.")]
        [SerializeField, Min(1)] private int blackoutAfterStacks = 5;
        [SerializeField, Min(0.1f)] private float drinkSeconds = 0.9f;
        [SerializeField, Min(0.1f)] private float drinkCooldown = 1.2f;
        [SerializeField, Min(0.1f)] private float secondsPerStack = 15f;
        [SerializeField, Min(0.1f)] private float respawnSeconds = 1.5f;

        private Health health;
        private PlayerInventory inventory;
        private FirstPersonController movement;
        private CharacterController body;
        private InputActionAsset runtimeActions;
        private InputActionMap playerMap;
        private InputAction interact, drink;
        private Vector3 spawnPosition, flaskRest;
        private Quaternion spawnRotation, flaskRotation;
        private GameObject enemySnapshot;
        private VolumeProfile runtimeProfile;
        private LensDistortion distortion;
        private ChromaticAberration aberration;
        private float nextDrink, drinkStarted, nextSoberStack;
        private bool sipApplied, scannerWasRaised;

        public bool HasLaser => inventory.Has(PlayerItem.MiningLaser);
        public bool HasFlask => inventory.Has(PlayerItem.Flask);
        public bool HasScanner => inventory.Has(PlayerItem.Scanner);
        public bool ScannerRaised => scanner != null && scanner.IsRaised;
        public ScannerToggle Scanner => scanner;
        public MiningLaser Laser => laser;
        public bool IsDrinking { get; private set; }
        public bool IsRespawning { get; private set; }
        public int DrunkStacks { get; private set; }
        public Health Health => health;
        public int BlackoutAfterStacks => blackoutAfterStacks;
        /// <summary>Health the next sip restores: full while sober, halved for every drunk stack.</summary>
        public float NextSipHeal => healAmount * Mathf.Pow(0.5f, DrunkStacks);
        /// <summary>Extra hangover blur (0 to 1) for the wake-up scene; only visual, it never slows movement.</summary>
        public float ExtraDaze { get; set; }
        /// <summary>Raised after a pickup is collected.</summary>
        public event Action<TutorialPickup.Item> Collected;
        /// <summary>Raised after a death or blackout has returned the player to the start.</summary>
        public event Action Respawned;

        private void Awake()
        {
            health = GetComponent<Health>();
            movement = GetComponent<FirstPersonController>();
            body = GetComponent<CharacterController>();
            inventory = GetComponent<PlayerInventory>();
            if (inventory == null) inventory = gameObject.AddComponent<PlayerInventory>();
            if (inputActions == null || view == null || laser == null || combatHud == null || heldFlask == null ||
                enemies == null || drunkVolume == null || hud == null || hud.healthSlider == null || healthText == null ||
                promptText == null || drunkText == null || messageText == null || blackoutOverlay == null)
            {
                Debug.LogError("TutorialPlayer scene references are incomplete; run TutorialGameplayBuilder.Validate.", this);
                enabled = false;
                return;
            }
            runtimeActions = Instantiate(inputActions);
            playerMap = runtimeActions.FindActionMap("Player", true);
            playerMap.bindingMask = InputBinding.MaskByGroups("Keyboard&Mouse", "Gamepad");
            interact = playerMap.FindAction("Interact", true);
            drink = playerMap.FindAction("Drink", true);
            laser.enabled = combatHud.enabled = false;
            if (scanner != null) scanner.enabled = false; // found in the wreckage during the tutorial
            flaskRest = heldFlask.localPosition;
            flaskRotation = heldFlask.localRotation;
            heldFlask.gameObject.SetActive(false);
            runtimeProfile = drunkVolume.profile;
            if (!runtimeProfile.TryGet(out distortion) || !runtimeProfile.TryGet(out aberration))
            {
                Debug.LogError("Tutorial drunk profile requires LensDistortion and ChromaticAberration.", this);
                enabled = false;
                return;
            }
            drunkVolume.weight = 0f;
            view.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        private void OnEnable()
        {
            playerMap.Enable();
            health.Changed += RefreshHealth;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            playerMap?.Disable();
            health.Changed -= RefreshHealth;
            health.Died -= OnDied;
        }

        private void Start()
        {
            DialogueUi.Instance.BindPlayer(transform);
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            // An inactive stage-start copy also restores enemies destroyed or hidden by death.
            enemySnapshot = new GameObject("Stage-start enemy snapshot");
            enemySnapshot.SetActive(false);
            Instantiate(enemies, enemySnapshot.transform, true);
            RefreshHealth(health);
        }

        private void OnDestroy()
        {
            if (runtimeActions != null) Destroy(runtimeActions);
            if (runtimeProfile != null) Destroy(runtimeProfile);
            if (enemySnapshot != null) Destroy(enemySnapshot);
        }

        private void Update()
        {
            if (IsRespawning) return;
            bool canControl = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
            TutorialPickup pickup = FindPickup();
            NpcActor npc = FindNpc();
            promptText.text = UiMenus.AnyOpen ? "" : npc != null ? npc.Prompt : pickup != null ? $"E  Pick up {pickup.DisplayName}" : "";
            if (canControl && !UiMenus.AnyOpen && !UiMenus.ChangedThisFrame && !IsDrinking && interact.WasPressedThisFrame()) TryInteract();
            if (canControl && drink.WasPressedThisFrame()) TryDrink();

            if (IsDrinking)
            {
                float t = Mathf.Clamp01((Time.time - drinkStarted) / drinkSeconds);
                float lift = Mathf.Sin(t * Mathf.PI);
                heldFlask.localPosition = Vector3.Lerp(flaskRest, new Vector3(-0.09f, -0.12f, 0.28f), lift);
                heldFlask.localRotation = flaskRotation * Quaternion.Euler(-105f * lift, 0f, -15f * lift);
                if (!sipApplied && t >= 0.5f)
                {
                    sipApplied = true;
                    health.Heal(NextSipHeal);
                    DrunkStacks++;
                    nextSoberStack = Time.time + secondsPerStack;
                    if (DrunkStacks > blackoutAfterStacks) BeginRespawn("BLACKOUT — too much Scotch");
                }
                if (!IsRespawning && t >= 1f)
                {
                    IsDrinking = false;
                    heldFlask.gameObject.SetActive(false);
                    LowerScanner();
                    SetWeaponActive();
                }
            }

            if (!IsRespawning && DrunkStacks > 0 && Time.time >= nextSoberStack)
            {
                DrunkStacks--;
                nextSoberStack = Time.time + secondsPerStack;
            }
            ApplyIntoxication();
        }

        private void LateUpdate()
        {
            // Q (ScannerToggle) raises the scanner; the laser goes away while it is up and comes back when lowered.
            if (ScannerRaised != scannerWasRaised)
            {
                scannerWasRaised = ScannerRaised;
                SetWeaponActive();
            }
            if (IsRespawning) return;
            drunkText.text = DrunkStacks > 0
                ? $"DRUNK x{DrunkStacks} / {blackoutAfterStacks}   •   Next sip +{NextSipHeal:0.#} HP" : "SOBER";
            messageText.text = UiMenus.AnyOpen ? "" : HasFlask ? $"F: drink Scotch (+{NextSipHeal:0.#} HP)   •   Q: swap scanner / laser" :
                HasScanner || HasLaser ? (HasScanner && HasLaser ? "Q: swap between scanner and laser" : "") : "";
            if (quest != null) { hud.SetObjective(quest.Accepted || UiMenus.AnyOpen ? "" : quest.Objective); return; }
            hud.SetObjective(!HasLaser ? "Recover the mining laser from the basin (E)." :
                !HasFlask ? "Recover your flask (E)." : "Try your laser on the targets; the red pad deals damage.");
        }

        private TutorialPickup FindPickup()
        {
            if (!Physics.Raycast(view.transform.position, view.transform.forward, out RaycastHit hit,
                    pickupDistance, ~0, QueryTriggerInteraction.Ignore)) return null;
            return hit.collider.GetComponentInParent<TutorialPickup>();
        }

        private NpcActor FindNpc()
        {
            if (!Physics.Raycast(view.transform.position, view.transform.forward, out RaycastHit hit,
                pickupDistance, ~0, QueryTriggerInteraction.Ignore)) return null;
            NpcActor actor = hit.collider.GetComponentInParent<NpcActor>();
            return actor != null && actor.CanInteract ? actor : null;
        }

        public bool TryInteract()
        {
            if (IsRespawning || health.IsDead || IsDrinking) return false;
            if (DialogueUi.Instance.PopupOpen) return DialogueUi.Instance.Advance();
            NpcActor npc = FindNpc();
            if (npc != null) return npc.Interact(this);
            TutorialPickup pickup = FindPickup();
            return pickup != null && pickup.TryCollect(this);
        }

        public bool Collect(TutorialPickup.Item item)
        {
            if (IsRespawning || health.IsDead) return false;
            PlayerItem gear = item == TutorialPickup.Item.Laser ? PlayerItem.MiningLaser :
                item == TutorialPickup.Item.Scanner ? PlayerItem.Scanner : PlayerItem.Flask;
            if (!inventory.Add(gear)) return false;
            if (gear == PlayerItem.Scanner && scanner != null) scanner.enabled = !IsDrinking;
            SetWeaponActive();
            IntroduceItem(gear);
            Collected?.Invoke(item);
            return true;
        }

        // A short card explaining what the new gear does and how to use it (meeting notes item 4).
        private void IntroduceItem(PlayerItem item)
        {
            GameplayOverlay overlay = GameplayOverlay.Instance;
            if (item == PlayerItem.MiningLaser)
            {
                string combo = laser.ComboCooldown > 0f ? $" The last hit stuns small critters, then needs {laser.ComboCooldown:0.#} s to recharge." : "";
                overlay.ShowItemCard("Mining Laser",
                    "Your trusty, slightly rusty multitool.\n" +
                    $"<b>Left click</b>  three-hit combo.{combo}\n" +
                    $"<b>Hold right click</b>  charge a bolt, release to fire. {laser.ShotCooldown:0.#} s to cool down.",
                    UiTheme.Accent);
            }
            else if (item == PlayerItem.Scanner)
            {
                overlay.ShowItemCard("Scanner",
                    "Sniffs out ship junk, critters and anyone with a job for you.\n" +
                    "<b>Q</b>  raise the scanner (the laser goes away). <b>Q</b> again to swap back.\n" +
                    "The arrow points at your current objective; the bar fills as you get close.",
                    UiTheme.SpeakerColor("Cavendish"));
            }
            else
            {
                overlay.ShowItemCard("Scotch Flask",
                    "Kirkland Signature. Medicinal. Probably.\n" +
                    $"<b>F</b>  take a sip: +{healAmount:0} HP while sober, half as much for every drink still in your system.\n" +
                    $"More than {blackoutAfterStacks} sips in a row and you black out.",
                    new Color(1f, 0.78f, 0.34f));
            }
        }

        public bool TryDrink()
        {
            if (!HasFlask || health.IsDead || IsRespawning || IsDrinking || Time.time < nextDrink) return false;
            IsDrinking = true;
            sipApplied = false;
            drinkStarted = Time.time;
            nextDrink = Time.time + drinkCooldown;
            heldFlask.localPosition = flaskRest;
            heldFlask.localRotation = flaskRotation;
            heldFlask.gameObject.SetActive(true);
            LowerScanner();
            SetWeaponActive();
            return true;
        }

        private void SetWeaponActive()
        {
            bool active = HasLaser && !IsDrinking && !IsRespawning && !health.IsDead && !ScannerRaised;
            laser.enabled = combatHud.enabled = active;
        }

        // Disabling ScannerToggle lowers it; it is re-enabled lowered, so the laser comes back out.
        private void LowerScanner()
        {
            if (scanner == null) return;
            scanner.enabled = false;
            scanner.enabled = HasScanner && !IsDrinking && !IsRespawning;
            scannerWasRaised = false;
        }

        private void RefreshHealth(Health value)
        {
            hud.UpdateHealth(value.Current, value.Max);
            healthText.text = $"HEALTH   {Mathf.CeilToInt(value.Current)} / {Mathf.CeilToInt(value.Max)}";
        }

        private void ApplyIntoxication()
        {
            float strength = Mathf.Clamp01(DrunkStacks / (float)blackoutAfterStacks);
            movement.Impairment = strength;
            drunkVolume.weight = Mathf.Max(strength, ExtraDaze);
            distortion.intensity.value = -0.4f - Mathf.Sin(Time.time * 1.4f) * 0.16f;
            aberration.intensity.value = 1f;
        }

        private void OnDied(Health _) => BeginRespawn("YOU DIED");

        private void BeginRespawn(string reason)
        {
            if (!IsRespawning) StartCoroutine(Respawn(reason));
        }

        private IEnumerator Respawn(string reason)
        {
            IsRespawning = true;
            DialogueUi.Instance.CloseAll();
            IsDrinking = false;
            promptText.text = "";
            messageText.text = reason + " — returning to the crash site";
            heldFlask.gameObject.SetActive(false);
            LowerScanner();
            SetWeaponActive();
            movement.enabled = false;
            float started = Time.time;
            while (Time.time - started < respawnSeconds)
            {
                blackoutOverlay.color = new Color(0f, 0f, 0f, Mathf.Clamp01((Time.time - started) / 0.4f));
                yield return null;
            }
            body.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            body.enabled = true;
            movement.ResetMotion();
            DrunkStacks = 0;
            ApplyIntoxication();
            health.Restore();
            foreach (LaserBolt bolt in FindObjectsByType<LaserBolt>())
                if (bolt.gameObject.scene == gameObject.scene) Destroy(bolt.gameObject);
            // Enemy projectiles belong to the previous attempt, just like player bolts.
            foreach (GooBlob blob in FindObjectsByType<GooBlob>())
                if (blob.gameObject.scene == gameObject.scene) Destroy(blob.gameObject);
            foreach (GooPuddle puddle in FindObjectsByType<GooPuddle>())
                if (puddle.gameObject.scene == gameObject.scene) Destroy(puddle.gameObject);
            Transform enemyParent = enemies.transform.parent;
            enemies.SetActive(false);
            Destroy(enemies);
            enemies = Instantiate(enemySnapshot.transform.GetChild(0).gameObject, enemyParent, true);
            enemies.name = "Tutorial Targets";
            enemies.SetActive(true);
            quest?.OnStageReset();
            blackoutOverlay.color = Color.clear;
            nextDrink = Time.time;
            IsRespawning = false;
            movement.enabled = true;
            LowerScanner();
            SetWeaponActive();
            Respawned?.Invoke();
        }
    }
}
