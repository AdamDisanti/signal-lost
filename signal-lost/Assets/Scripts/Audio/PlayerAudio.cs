using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.Scanner;
using SignalLost.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SignalLost.Audio
{
    /// <summary>
    /// The player's sounds, added automatically by <see cref="SoundDirector"/>. It only watches the existing player
    /// components (laser, gear, flask, life, inventory, controller) and never changes how they behave:
    /// laser swings/slam, charge hum and full-charge ding, shots, bolt impacts and the full-charge blast, cooldown
    /// fizzle, hit marker, footsteps/jump/landing, hurt grunts (plus the crab's claw snip), death and blackout,
    /// flask uncork/glug/"ahh"/hiccup/burp, scanner raise/lower, item pickups, scrap pickups and the wake-up ringing.
    /// </summary>
    [DefaultExecutionOrder(1000)] // after the laser's LateUpdate, so a shot and its cooldown show up the same frame
    public sealed class PlayerAudio : MonoBehaviour
    {
        private MiningLaser laser;
        private PlayerGear gear;
        private PlayerFlask flask;
        private PlayerLife life;
        private Health health;
        private PlayerInventory inventory;
        private ScannerToggle scanner;
        private CharacterController body;
        private WakeUpSequence wakeUp;

        private SfxLoop chargeLoop, heartbeat;
        private AudioSource ringing;
        private string surface = "dirt";

        // Previous-frame state, to spot changes.
        private bool wasSwinging, wasCharging, chargeWasFull, wasDrinking, scannerWasUp, wasGrounded = true, wasWaking;
        private int swingCount;
        private float lastCharge, lastHitSeen = float.NegativeInfinity, lastFullShot = -99f;
        private float stride, airTime, fallSpeed, nextHurt, nextFizzle, nextClink, nextVacuum, nextHiccup;
        private readonly Dictionary<LaserBolt, Vector3> bolts = new Dictionary<LaserBolt, Vector3>();
        private readonly List<LaserBolt> gone = new List<LaserBolt>();

        private void Awake()
        {
            laser = GetComponent<MiningLaser>();
            gear = GetComponent<PlayerGear>();
            flask = GetComponent<PlayerFlask>();
            life = GetComponent<PlayerLife>();
            health = GetComponent<Health>();
            inventory = GetComponent<PlayerInventory>();
            if (inventory == null) inventory = PlayerInventory.Current;
            scanner = gear != null && gear.Scanner != null ? gear.Scanner : GetComponentInChildren<ScannerToggle>(true);
            body = GetComponent<CharacterController>();
            wakeUp = FindAnyObjectByType<WakeUpSequence>();

            string scene = SceneManager.GetActiveScene().name.ToLowerInvariant();
            surface = scene.Contains("desert") ? "sand" : scene.Contains("tutorial") ? "grass" : "dirt";

            chargeLoop = Sfx.AddLoop(gameObject, "laser_charge", 0.45f, false, childName: "Laser Charge Sound");
            if (chargeLoop != null)
            {
                chargeLoop.Source.loop = false;
                chargeLoop.Source.Stop();
            }
            heartbeat = Sfx.AddLoop(gameObject, "low_health_heartbeat", 0.7f, false, childName: "Low Health Heartbeat");
            if (heartbeat != null) heartbeat.Level = 0f;
            nextHiccup = Time.time + 8f;
            if (laser != null) lastHitSeen = laser.LastHitTime;
        }

        private void OnEnable()
        {
            if (health != null) health.Damaged += OnHurt;
            if (life != null) life.RespawnStarted += OnRespawnStarted;
            else if (health != null) health.Died += OnDied;
            if (flask != null) flask.Sipped += OnSipped;
            if (gear != null) gear.Collected += OnCollected;
            if (inventory != null) inventory.ScrapChanged += OnScrapChanged;
        }

        private void OnDisable()
        {
            if (health != null) { health.Damaged -= OnHurt; health.Died -= OnDied; }
            if (life != null) life.RespawnStarted -= OnRespawnStarted;
            if (flask != null) flask.Sipped -= OnSipped;
            if (gear != null) gear.Collected -= OnCollected;
            if (inventory != null) inventory.ScrapChanged -= OnScrapChanged;
            StopCharge();
        }

        private void LateUpdate()
        {
            UpdateLaser();
            UpdateBolts();
            UpdateMovement();
            UpdateScanner();
            UpdateFlask();
            UpdateWakeUp();
            UpdateHeartbeat();
        }

        // A heartbeat under everything while health is at or below the low-health line (the HUD pulses with it).
        private void UpdateHeartbeat()
        {
            if (heartbeat == null || health == null) return;
            bool low = !health.IsDead && health.Current <= SignalLost.UI.GameHud.LowHealth;
            float target = low ? Mathf.Lerp(1f, 0.55f, health.Current / SignalLost.UI.GameHud.LowHealth) : 0f;
            heartbeat.Level = Mathf.MoveTowards(heartbeat.Level, target, Time.unscaledDeltaTime * 1.5f);
        }

        // ---------- Mining laser ----------

        private void UpdateLaser()
        {
            if (laser == null) return;

            // Swings: two alternating whooshes, and the heavier slam on the last hit of the combo.
            if (laser.IsSwinging && !wasSwinging)
            {
                bool finisher = laser.ComboStep >= laser.ComboLength;
                if (finisher) Sfx.Play("laser_slam", 0.8f, Random.Range(0.96f, 1.04f));
                else Sfx.Play(++swingCount % 2 == 0 ? "laser_swing_2" : "laser_swing_1", 0.55f, Random.Range(0.94f, 1.08f));
            }
            wasSwinging = laser.IsSwinging;

            // Charging: a rising hum, a ding at full charge, then a steady crackling hum until release.
            if (laser.IsCharging)
            {
                lastCharge = laser.Charge01;
                if (!wasCharging) StartCharge();
                if (lastCharge >= 1f && !chargeWasFull)
                {
                    chargeWasFull = true;
                    Sfx.Play("laser_charge_full", 0.6f);
                    if (chargeLoop != null)
                    {
                        chargeLoop.SetClip("laser_charge_hold");
                        chargeLoop.Source.loop = true;
                        chargeLoop.Level = 0.8f;
                    }
                }
            }
            else if (wasCharging)
            {
                StopCharge();
                // Released into a shot (the laser starts its cooldown the same frame); otherwise the charge was cancelled.
                if (laser.IsCoolingDown)
                {
                    bool full = lastCharge >= 1f;
                    if (full) lastFullShot = Time.time;
                    Sfx.Play(lastCharge < 0.55f ? "laser_shot_weak" : "laser_shot_strong", 0.55f + 0.35f * lastCharge,
                        Random.Range(0.97f, 1.03f));
                }
            }
            wasCharging = laser.IsCharging;

            // Clicking while the laser can't act yet.
            Mouse mouse = Mouse.current;
            if (mouse != null && laser.isActiveAndEnabled && Time.time >= nextFizzle && !UiMenus.AnyOpen &&
                Cursor.lockState == CursorLockMode.Locked)
            {
                bool blockedShot = mouse.rightButton.wasPressedThisFrame && laser.IsCoolingDown;
                bool blockedSwing = mouse.leftButton.wasPressedThisFrame && laser.ComboCooldownRemaining > 0f && !laser.IsSwinging;
                if (blockedShot || blockedSwing)
                {
                    Sfx.Play("laser_fizzle", 0.45f, Random.Range(0.95f, 1.05f));
                    nextFizzle = Time.time + 0.25f;
                }
            }

            if (laser.LastHitTime > lastHitSeen)
            {
                Sfx.Play("hit_marker", 0.35f, Random.Range(0.95f, 1.08f));
                lastHitSeen = laser.LastHitTime;
            }
        }

        private void StartCharge()
        {
            chargeWasFull = false;
            if (chargeLoop == null) return;
            chargeLoop.SetClip("laser_charge");
            chargeLoop.Source.loop = false;
            chargeLoop.Level = 1f;
            chargeLoop.Source.time = 0f;
            chargeLoop.Source.Play();
        }

        private void StopCharge()
        {
            chargeWasFull = false;
            if (chargeLoop != null && chargeLoop.Source != null) chargeLoop.Source.Stop();
        }

        /// <summary>Bolts are tracked while they fly; when one disappears it has hit something (or run out of range).</summary>
        private void UpdateBolts()
        {
            gone.Clear();
            foreach (KeyValuePair<LaserBolt, Vector3> pair in bolts)
                if (pair.Key == null || !pair.Key.isActiveAndEnabled) gone.Add(pair.Key);
            foreach (LaserBolt bolt in gone)
            {
                Vector3 where = bolts[bolt];
                bolts.Remove(bolt);
                bool blast = Time.time - lastFullShot < 4f;
                if (blast) lastFullShot = -99f;
                Sfx.PlayAt(blast ? "laser_blast" : "bolt_impact", where, blast ? 0.95f : 0.65f, Random.Range(0.95f, 1.05f), blast ? 70f : 35f);
            }
            foreach (LaserBolt bolt in FindObjectsByType<LaserBolt>())
                bolts[bolt] = bolt.transform.position;
        }

        // ---------- Footsteps, jumping, landing ----------

        private void UpdateMovement()
        {
            if (body == null || !body.enabled) return;
            bool grounded = body.isGrounded;
            Vector3 velocity = body.velocity;
            float speed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
            bool busy = UiMenus.FullScreenOpen || (life != null && life.IsRespawning) || Time.timeScale == 0f;

            if (grounded && !busy && speed > 0.6f)
            {
                stride += speed * Time.deltaTime;
                float strideLength = speed > 5.5f ? 2.4f : 1.9f;
                if (stride >= strideLength)
                {
                    stride = 0f;
                    Sfx.Play(Sfx.Pick("footstep_" + surface, 4), speed > 5.5f ? 0.38f : 0.28f, Random.Range(0.9f, 1.1f));
                }
            }
            else if (grounded) stride = 1.5f; // first step comes quickly after starting to move

            if (!grounded)
            {
                airTime += Time.deltaTime;
                fallSpeed = Mathf.Min(fallSpeed, velocity.y);
                if (wasGrounded && velocity.y > 1.5f && !busy) Sfx.Play("player_jump", 0.35f, Random.Range(0.95f, 1.05f));
            }
            else
            {
                if (!wasGrounded && airTime > 0.3f && fallSpeed < -3.5f && !busy)
                    Sfx.Play("player_land", Mathf.Clamp(0.25f - fallSpeed * 0.04f, 0.3f, 0.8f), Random.Range(0.95f, 1.05f));
                airTime = 0f;
                fallSpeed = 0f;
            }
            wasGrounded = grounded;
        }

        // ---------- Scanner, items, scrap ----------

        private void UpdateScanner()
        {
            bool up = gear != null ? gear.ScannerRaised : scanner != null && scanner.isActiveAndEnabled && scanner.IsRaised;
            if (up != scannerWasUp) Sfx.Play(up ? "scanner_raise" : "scanner_lower", 0.45f);
            scannerWasUp = up;
        }

        private void OnCollected(PlayerItem item) => Sfx.Play("item_pickup", 0.6f);

        private void OnScrapChanged(ScrapType type, int change, int total)
        {
            if (change < 0)
            {
                Sfx.Play("shop_chaching", 0.7f, 1f, SoundBus.Interface); // spent at the merchant
                return;
            }
            if (Time.time >= nextVacuum)
            {
                Sfx.Play("scrap_vacuum", 0.3f, Random.Range(0.95f, 1.1f));
                nextVacuum = Time.time + 0.35f;
            }
            if (Time.time >= nextClink)
            {
                Sfx.Play(Sfx.Pick("scrap_clink", 3), 0.35f, Random.Range(0.9f, 1.15f));
                nextClink = Time.time + 0.07f;
            }
        }

        // ---------- Flask ----------

        private void UpdateFlask()
        {
            if (flask == null) return;
            if (flask.IsDrinking && !wasDrinking)
            {
                Sfx.Play("flask_uncork", 0.55f, Random.Range(0.95f, 1.05f));
                Invoke(nameof(Glug), 0.15f);
            }
            wasDrinking = flask.IsDrinking;

            // A tipsy mechanic hiccups now and then.
            if (flask.Drunkenness >= 0.35f && Time.time >= nextHiccup && !flask.IsDrinking && !UiMenus.FullScreenOpen)
            {
                Sfx.Play("flask_hiccup", 0.5f, Random.Range(0.92f, 1.1f));
                nextHiccup = Time.time + Random.Range(5f, 11f) * (1.4f - flask.Drunkenness);
            }
            else if (flask.Drunkenness < 0.35f && Time.time >= nextHiccup) nextHiccup = Time.time + 4f;
        }

        private void Glug() => Sfx.Play("flask_glug", 0.55f, Random.Range(0.95f, 1.05f));

        private void OnSipped(float healed) => Invoke(nameof(AfterSip), 0.35f);

        private void AfterSip()
        {
            float drunk = flask != null ? flask.Drunkenness : 0f;
            // Every sip from the third on ends in a burp (meeting notes 10-23).
            if (flask != null && flask.DrunkStacks >= 3) Sfx.Play("flask_burp", 0.6f, Random.Range(0.9f, 1.05f));
            else if (drunk >= 0.35f) Sfx.Play("flask_hiccup", 0.5f);
            else Sfx.Play("flask_ahh", 0.5f, Random.Range(0.97f, 1.03f));
        }

        // ---------- Getting hurt, dying, waking up ----------

        private void OnHurt(Health _, float amount, GameObject source)
        {
            if (source != null && source.GetComponentInParent<HermitCrab>() != null)
                Sfx.PlayAt("crab_snip", source.transform.position + Vector3.up * 0.5f, 0.75f, Random.Range(0.92f, 1.08f), 25f);
            if (Time.time < nextHurt || health == null || health.IsDead) return;
            Sfx.Play(Sfx.Pick("player_hurt", 3), 0.7f, Random.Range(0.94f, 1.06f));
            nextHurt = Time.time + 0.35f;
        }

        private void OnRespawnStarted(string message)
        {
            StopCharge();
            bool blackout = message != null && message.ToUpperInvariant().Contains("BLACKOUT");
            Sfx.Play(blackout ? "blackout_whoosh" : "death_sting", 0.8f);
        }

        private void OnDied(Health _)
        {
            StopCharge();
            Sfx.Play("death_sting", 0.8f);
        }

        private void UpdateWakeUp()
        {
            bool waking = wakeUp != null && wakeUp.Playing;
            if (waking && !wasWaking)
            {
                if (ringing != null) ringing.Stop();
                ringing = Sfx.Play("wake_ringing", 0.55f);
            }
            else if (!waking && wasWaking && ringing != null && ringing.clip != null && ringing.clip.name == "wake_ringing")
            {
                ringing.Stop();
                ringing = null;
            }
            wasWaking = waking;
        }
    }
}
