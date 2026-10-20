using System.Collections;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.NPC;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SignalLost.Player
{
    [RequireComponent(typeof(Health), typeof(FirstPersonController))]
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
        [SerializeField] private TutorialNpcUi npcUi;

        [Header("Prototype tuning")]
        [SerializeField, Min(0.5f)] private float pickupDistance = 3f;
        [SerializeField, Min(1f)] private float healAmount = 35f;
        [SerializeField, Min(0.1f)] private float drinkSeconds = 0.9f;
        [SerializeField, Min(0.1f)] private float drinkCooldown = 1.2f;
        [SerializeField, Min(0.1f)] private float secondsPerStack = 15f;
        [SerializeField, Min(0.1f)] private float respawnSeconds = 1.5f;

        private Health health;
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
        private bool sipApplied;

        public bool HasLaser { get; private set; }
        public bool HasFlask { get; private set; }
        public bool IsDrinking { get; private set; }
        public bool IsRespawning { get; private set; }
        public int DrunkStacks { get; private set; }
        public Health Health => health;

        private void Awake()
        {
            health = GetComponent<Health>();
            movement = GetComponent<FirstPersonController>();
            body = GetComponent<CharacterController>();
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

        public void BindNpcUi(TutorialNpcUi windows)
        {
            npcUi = windows;
            npcUi?.BindPlayer(transform);
        }

        private void Start()
        {
            if (npcUi == null) BindNpcUi(FindFirstObjectByType<TutorialNpcUi>());
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
            promptText.text = npcUi != null && npcUi.HasOpenWindow ? "" : npc != null ? npc.Prompt : pickup != null ? $"E  Pick up {pickup.DisplayName}" : "";
            if (canControl && (npcUi == null || (!npcUi.HasOpenWindow && !npcUi.WindowChangedThisFrame)) && !IsDrinking && interact.WasPressedThisFrame()) TryInteract();
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
                    health.Heal(healAmount);
                    DrunkStacks++;
                    nextSoberStack = Time.time + secondsPerStack;
                    if (DrunkStacks > 8) BeginRespawn("BLACKOUT — too much Scotch");
                }
                if (!IsRespawning && t >= 1f)
                {
                    IsDrinking = false;
                    heldFlask.gameObject.SetActive(false);
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
            if (IsRespawning) return;
            drunkText.text = DrunkStacks > 0
                ? $"DRUNK x{DrunkStacks} / 8   •   Blackout above 8" : "SOBER";
            messageText.text = HasFlask ? $"F: drink Scotch (+{healAmount:0} HP)   •   Unlimited uses" :
                quest != null ? "Talk to Cavendish to recover your flask." : "Find the flask near the basin";
            if (quest != null) { hud.SetObjective(npcUi != null && (quest.Accepted || npcUi.HasOpenWindow) ? "" : quest.Objective); return; }
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
            if (npcUi != null && npcUi.HasOpenWindow) return npcUi.Advance();
            NpcActor npc = FindNpc();
            if (npc != null) return npc.Interact(this);
            TutorialPickup pickup = FindPickup();
            return pickup != null && pickup.TryCollect(this);
        }

        public bool Collect(TutorialPickup.Item item)
        {
            if (IsRespawning || health.IsDead) return false;
            if (item == TutorialPickup.Item.Laser)
            {
                if (HasLaser) return false;
                HasLaser = true;
            }
            else
            {
                if (HasFlask) return false;
                HasFlask = true;
            }
            SetWeaponActive();
            return true;
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
            SetWeaponActive();
            return true;
        }

        private void SetWeaponActive()
        {
            bool active = HasLaser && !IsDrinking && !IsRespawning && !health.IsDead;
            laser.enabled = combatHud.enabled = active;
        }

        private void RefreshHealth(Health value)
        {
            hud.UpdateHealth(value.Current, value.Max);
            healthText.text = $"HEALTH   {Mathf.CeilToInt(value.Current)} / {Mathf.CeilToInt(value.Max)}";
        }

        private void ApplyIntoxication()
        {
            float strength = Mathf.Clamp01(DrunkStacks / 8f);
            movement.Impairment = strength;
            drunkVolume.weight = strength;
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
            npcUi?.CloseAll();
            IsDrinking = false;
            promptText.text = "";
            messageText.text = reason + " — returning to the crash site";
            heldFlask.gameObject.SetActive(false);
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
            SetWeaponActive();
        }
    }
}
