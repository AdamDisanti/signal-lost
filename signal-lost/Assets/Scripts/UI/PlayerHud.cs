using SignalLost.Combat;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost.UI
{
    /// <summary>
    /// The player's in-game HUD (design doc HUD list): health bar, scanner direction and signal strength
    /// and objective tracker, plus a crosshair with hit marker, laser charge bar and target readout.
    ///
    /// Lives on the root of the PlayerHUD prefab (Assets/Prefabs/UI). Drop the prefab into any scene with the
    /// player and it finds the player's Health, MiningLaser and camera by itself; you can also drag them in.
    ///
    /// Health and laser are read automatically. Systems that don't exist yet push their values in through
    /// PlayerHud.Current, for example:
    ///     PlayerHud.Current?.SetObjective("Defeat creatures (1/3)");  // quests / encounters
    ///     PlayerHud.Current?.SetScannerSignal(target.position, strength01); // scanner
    ///     PlayerHud.Current?.ClearScannerSignal();                // scanner has nothing to point at
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHud : MonoBehaviour
    {
        /// <summary>The active HUD in the scene, or null if there is none.</summary>
        public static PlayerHud Current { get; private set; }

        [Header("What the HUD reads (found automatically if left empty)")]
        [SerializeField] private Health playerHealth;
        [SerializeField] private MiningLaser laser;
        [Tooltip("Usually the player camera. Used to work out which way the scanner arrow points.")]
        [SerializeField] private Transform viewpoint;

        [Header("Health (top left)")]
        [SerializeField] private RectTransform healthFill;
        [SerializeField] private RectTransform healthTrail;
        [SerializeField] private RectTransform overhealFill;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private Text healthText;
        [SerializeField] private Image hurtFlash;

        [Header("Crosshair and laser (center)")]
        [SerializeField] private RectTransform crosshair;
        [SerializeField] private Image[] crosshairParts;
        [SerializeField] private GameObject chargeGroup;
        [SerializeField] private RectTransform chargeFill;
        [SerializeField] private Image chargeFillImage;
        [SerializeField] private Text chargeText;
        [SerializeField] private GameObject targetGroup;
        [SerializeField] private Text targetName;
        [SerializeField] private RectTransform targetFill;

        [Header("Scanner (top center)")]
        [SerializeField] private GameObject scannerNeedleGroup;
        [SerializeField] private RectTransform scannerNeedle;
        [SerializeField] private Image[] signalPips;
        [SerializeField] private Text scannerStatus;

        [Header("Objective (top right)")]
        [SerializeField] private GameObject objectiveGroup;
        [SerializeField] private Text objectiveText;

        [Header("Center message (death, paused)")]
        [SerializeField] private Text centerMessage;

        [Header("Colors")]
        [SerializeField] private Color healthColor = new Color(0.45f, 0.9f, 0.45f);
        [SerializeField] private Color lowHealthColor = new Color(0.95f, 0.3f, 0.25f);
        [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.25f;
        [SerializeField] private Color crosshairColor = Color.white;
        [SerializeField] private Color hitMarkerColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color chargeColor = new Color(0.3f, 0.85f, 1f);
        [SerializeField] private Color fullChargeColor = new Color(1f, 0.55f, 0.2f);
        [SerializeField] private Color pipOnColor = new Color(0.3f, 0.85f, 1f);
        [SerializeField] private Color pipOffColor = new Color(1f, 1f, 1f, 0.15f);

        [Header("Feel")]
        [Tooltip("Seconds the pale 'damage taken' trail waits before shrinking to the new health.")]
        [SerializeField, Min(0f)] private float trailDelay = 0.4f;
        [SerializeField, Min(0.01f)] private float trailSpeed = 0.8f;
        [SerializeField, Min(0.01f)] private float hurtFlashTime = 0.35f;

        private float trailValue = 1f, trailWait, flash;
        private bool hasSignal;
        private Vector3 signalPosition;
        private float signalStrength;

        // ---------- Public API for other systems ----------

        /// <summary>Shows one line of objective text. Pass null or empty to hide the tracker.</summary>
        public void SetObjective(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            if (objectiveGroup != null) objectiveGroup.SetActive(show);
            if (objectiveText != null) objectiveText.text = show ? text : string.Empty;
        }

        /// <summary>Points the scanner arrow at a world position. Strength is 0 (faint) to 1 (right on top of it).</summary>
        public void SetScannerSignal(Vector3 worldPosition, float strength01)
        {
            hasSignal = true;
            signalPosition = worldPosition;
            signalStrength = Mathf.Clamp01(strength01);
        }

        /// <summary>Scanner has nothing to point at: arrow hides and it reads NO SIGNAL.</summary>
        public void ClearScannerSignal()
        {
            hasSignal = false;
            signalStrength = 0f;
        }

        // ---------- Unity ----------

        private void Awake()
        {
            FindMissingReferences();
            if (objectiveText != null) SetObjective(objectiveText.text); // keeps whatever text the prefab shows
            if (hurtFlash != null) hurtFlash.color = WithAlpha(hurtFlash.color, 0f);
        }

        private void OnEnable()
        {
            Current = this;
            if (playerHealth != null) playerHealth.Damaged += OnPlayerDamaged;
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
            if (playerHealth != null) playerHealth.Damaged -= OnPlayerDamaged;
        }

        private void OnPlayerDamaged(Health _, float amount, GameObject source)
        {
            flash = 1f;
            trailWait = trailDelay;
        }

        private void Update()
        {
            UpdateHealth();
            UpdateCenter();
            UpdateScanner();
        }

        // ---------- Pieces ----------

        private void UpdateHealth()
        {
            if (playerHealth == null) return;

            float max = Mathf.Max(1f, playerHealth.Max);
            float value = Mathf.Clamp01(playerHealth.Current / max);
            // Health above max (flask overheal) shows as an extra gold strip. Health.cs can't overheal yet;
            // this starts working automatically once it can.
            float overheal = Mathf.Clamp01((playerHealth.Current - max) / max);

            SetWidth(healthFill, value);
            SetWidth(overhealFill, overheal);
            if (healthFillImage != null)
                healthFillImage.color = value <= lowHealthThreshold
                    ? Color.Lerp(lowHealthColor, Color.white, 0.25f * (0.5f + 0.5f * Mathf.Sin(Time.time * 8f)))
                    : healthColor;
            if (healthText != null)
                healthText.text = $"{Mathf.CeilToInt(playerHealth.Current)} / {Mathf.CeilToInt(max)}";

            // Pale trail shows how much was just lost, then catches up.
            if (value >= trailValue) trailValue = value;
            else if ((trailWait -= Time.deltaTime) <= 0f) trailValue = Mathf.MoveTowards(trailValue, value, trailSpeed * Time.deltaTime);
            SetWidth(healthTrail, trailValue);

            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime / hurtFlashTime);
            if (hurtFlash != null) hurtFlash.color = WithAlpha(hurtFlash.color, 0.35f * flash);
        }

        private void UpdateCenter()
        {
            bool dead = playerHealth != null && playerHealth.IsDead;
            bool paused = !dead && Cursor.lockState != CursorLockMode.Locked;
            if (centerMessage != null)
            {
                centerMessage.gameObject.SetActive(dead || paused);
                centerMessage.text = dead ? "SIGNAL LOST\n<size=26>Respawning...</size>" : "Click to resume";
            }

            bool showCombat = !dead && !paused && laser != null && laser.isActiveAndEnabled;
            if (crosshair != null) crosshair.gameObject.SetActive(!dead && !paused);
            if (!showCombat)
            {
                if (chargeGroup != null) chargeGroup.SetActive(false);
                if (targetGroup != null) targetGroup.SetActive(false);
                return;
            }

            // Crosshair grows and turns yellow briefly after landing a hit.
            float sinceHit = Time.time - laser.LastHitTime;
            bool justHit = sinceHit < 0.15f;
            if (crosshair != null) crosshair.localScale = Vector3.one * (justHit ? 1.5f : 1f);
            if (crosshairParts != null)
                foreach (Image part in crosshairParts)
                    if (part != null) part.color = justHit ? hitMarkerColor : crosshairColor;

            // Charge bar only while charging or cooling down.
            bool charging = laser.IsCharging, cooling = laser.IsCoolingDown;
            if (chargeGroup != null) chargeGroup.SetActive(charging || cooling);
            if (charging)
            {
                float charge = laser.Charge01;
                bool full = charge >= 1f;
                SetWidth(chargeFill, charge);
                if (chargeFillImage != null) chargeFillImage.color = full ? fullChargeColor : chargeColor;
                if (chargeText != null) chargeText.text = full ? "FULL CHARGE" : "CHARGING";
            }
            else if (cooling)
            {
                SetWidth(chargeFill, 0f);
                if (chargeText != null) chargeText.text = "COOLING";
            }

            // Name and health of whatever enemy is under the crosshair.
            Health target = laser.CurrentTarget;
            bool showTarget = target != null && !target.IsDead && target != playerHealth;
            if (targetGroup != null) targetGroup.SetActive(showTarget);
            if (showTarget)
            {
                if (targetName != null) targetName.text = target.name;
                SetWidth(targetFill, target.Normalized);
            }
        }

        private void UpdateScanner()
        {
            if (scannerNeedleGroup != null) scannerNeedleGroup.SetActive(hasSignal);
            if (scannerStatus != null) scannerStatus.text = hasSignal ? "SIGNAL" : "NO SIGNAL";

            if (hasSignal && viewpoint != null && scannerNeedle != null)
            {
                // Angle on the ground between where you're looking and where the signal is. Right = clockwise.
                Vector3 forward = Vector3.ProjectOnPlane(viewpoint.forward, Vector3.up);
                Vector3 toSignal = Vector3.ProjectOnPlane(signalPosition - viewpoint.position, Vector3.up);
                if (forward.sqrMagnitude > 0.0001f && toSignal.sqrMagnitude > 0.0001f)
                {
                    float angle = Vector3.SignedAngle(forward, toSignal, Vector3.up);
                    scannerNeedle.localRotation = Quaternion.Euler(0f, 0f, -angle);
                }
            }

            if (signalPips == null) return;
            int lit = hasSignal ? Mathf.CeilToInt(signalStrength * signalPips.Length) : 0;
            for (int i = 0; i < signalPips.Length; i++)
                if (signalPips[i] != null) signalPips[i].color = i < lit ? pipOnColor : pipOffColor;
        }

        // ---------- Helpers ----------

        private void FindMissingReferences()
        {
            if (playerHealth != null && laser != null && viewpoint != null) return;
            var player = FindAnyObjectByType<FirstPersonController>();
            if (player == null) return;
            if (playerHealth == null) playerHealth = player.GetComponent<Health>();
            if (laser == null) laser = player.GetComponent<MiningLaser>();
            if (viewpoint == null)
            {
                Camera camera = player.GetComponentInChildren<Camera>();
                if (camera != null) viewpoint = camera.transform;
            }
        }

        /// <summary>Bars are stretched rectangles; this sets how far across their parent they reach (0 to 1).</summary>
        private static void SetWidth(RectTransform bar, float amount01)
        {
            if (bar == null) return;
            bar.anchorMax = new Vector2(Mathf.Clamp01(amount01), bar.anchorMax.y);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
