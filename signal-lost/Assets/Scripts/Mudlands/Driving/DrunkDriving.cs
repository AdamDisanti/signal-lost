using System;
using System.Collections;
using System.Collections.Generic;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.UI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// The Mudlands' new ability (docs/mudlands.md). Once the Learner's Permit is in the pockets, holding the drink
    /// button (F) turns the sip into a chug that drains the scotch flask; when it is empty a car drops out of
    /// the sky around the mechanic and they drive it (<see cref="DrunkCar"/>) until it blows up, then they are thrown
    /// clear and are back on foot.
    ///
    /// Cost and limits: the chug adds 2 drinks (no heal), cannot black you out (refused if it would), takes a 30 s
    /// cooldown after the ride, is spilled by a hit of 15+, and needs room to park. Upgrades (Car slot) change the
    /// numbers. Added to the player at runtime by <see cref="Ensure"/>, so the shared Player prefab is unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DrunkDriving : MonoBehaviour
    {
        public enum State { Ready, Chugging, Driving, Tumbling }

        public const float HoldToChug = 0.3f;
        private const float BaseChugSeconds = 1.6f, BaseCooldown = 30f, BaseRadius = 6f, BaseDamage = 60f, BaseStun = 2.5f;
        private const int BaseChugDrinks = 2;
        private const float SpillDamage = 15f;

        private PlayerFlask flask;
        private PlayerGear gear;
        private PlayerInventory pockets;
        private PlayerLife life;
        private Health health;
        private FirstPersonController movement;
        private CharacterController body;
        private PlayerInteractor interactor;
        private MudlandsPlayerSlow slow;
        private Camera view;
        private readonly List<Renderer> hidden = new List<Renderer>();
        private InputActionAsset actions;
        private InputAction drink, move, jump, attack;
        private float holdStarted = -1f, chugStarted, cooldownUntil, lastRefusal;
        private bool holdConsumed;
        private DrunkCar car;
        private ChaseCamera chase;
        private readonly HashSet<object> blockers = new HashSet<object>();

        public State Current { get; private set; } = State.Ready;
        public DrunkCar Car => car;
        public bool Unlocked => pockets != null && pockets.HasKeyItem(KeyItems.LearnersPermit);
        public float CooldownRemaining => Mathf.Max(0f, cooldownUntil - Time.time);
        public float CooldownLength { get; private set; } = BaseCooldown;
        public bool InCar => Current == State.Driving && car != null;

        // Upgrade-driven numbers (Car slot, plus Moonshine Distiller and Swamp Waders).
        public float ChugSeconds { get; private set; } = BaseChugSeconds;
        public float BumperMultiplier { get; private set; } = 1f;
        public bool Reinforced { get; private set; }
        public float BlastRadius { get; private set; } = BaseRadius;
        public float BlastDamage { get; private set; } = BaseDamage;
        public float StunSeconds { get; private set; } = BaseStun;
        public bool MudTires { get; private set; }
        public bool SpillProof { get; private set; }
        public int ChugDrinks { get; private set; } = BaseChugDrinks;
        public float RideSeconds => 14f;
        public float CarHealth => 80f;
        public float Drunkenness => flask != null ? flask.Drunkenness : 0f;

        /// <summary>
        /// The boss chase: rides cost nothing, have no cooldown, ignore the drink limit and never run dry, and the car
        /// is a little faster, so the ability can never be locked out when the story needs it.
        /// </summary>
        public bool FreeRides { get; set; }
        private bool freeRide; // one free ride (TryStartFree), cleared when that ride ends
        /// <summary>Whether the current or next ride costs nothing (the boss chase, or a one-off free ride).</summary>
        public bool Free => FreeRides || freeRide;

        // The chug's pose, read by FirstPersonHands: raise 0-1 brings the flask to the mouth, gulps counts swallows.
        public bool ChugActive => Current == State.Chugging;
        public float ChugRaise { get; private set; }
        public float ChugGulps { get; private set; }
        public float ChugProgress => Current == State.Chugging ? Mathf.Clamp01((Time.time - chugStarted) / ChugSeconds) : 0f;

        public event Action ChugStarted;
        public event Action<DrunkCar> CarSpawned;
        /// <summary>The ride ended. Arguments: the car, why, where.</summary>
        public event Action<DrunkCar, DrunkCar.EndReason, Vector3> RideEnded;
        /// <summary>Raised once the Learner's Permit lands in the pockets.</summary>
        public static event Action<DrunkDriving> PermitUnlocked;

        /// <summary>The player's Drunk Driving (one player).</summary>
        public static DrunkDriving Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            PermitUnlocked = null;
        }

        // Every scene's player gets the component; it does nothing until the permit is owned.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToPlayer()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureOnPlayer();
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => EnsureOnPlayer();

        private static void EnsureOnPlayer()
        {
            var flaskOwner = FindAnyObjectByType<PlayerFlask>();
            if (flaskOwner != null) Ensure(flaskOwner.gameObject);
        }

        public static DrunkDriving Ensure(GameObject player)
        {
            if (player == null) return null;
            var found = player.GetComponent<DrunkDriving>();
            return found != null ? found : player.AddComponent<DrunkDriving>();
        }

        // ---------- Unity ----------

        private void Awake()
        {
            Instance = this;
            flask = GetComponent<PlayerFlask>();
            gear = GetComponent<PlayerGear>();
            pockets = GetComponent<PlayerInventory>();
            life = GetComponent<PlayerLife>();
            health = GetComponent<Health>();
            movement = GetComponent<FirstPersonController>();
            body = GetComponent<CharacterController>();
            interactor = GetComponent<PlayerInteractor>();
            slow = MudlandsPlayerSlow.Ensure(gameObject);
            Camera found = GetComponentInChildren<Camera>(true);
            view = found;
            if (flask != null && flask.Actions != null)
            {
                actions = Instantiate(flask.Actions);
                InputActionMap map = actions.FindActionMap("Player", true);
                map.bindingMask = InputBinding.MaskByGroups("Keyboard&Mouse", "Gamepad");
                drink = map.FindAction("Drink", true);
                move = map.FindAction("Move", true);
                jump = map.FindAction("Jump", true);
                attack = map.FindAction("Attack", true);
            }
        }

        private void OnEnable()
        {
            actions?.FindActionMap("Player").Enable();
            if (Unlocked) MudHud.Ensure(); // it draws the car pip
            if (pockets != null)
            {
                pockets.Purchased += OnPurchased;
                pockets.Changed += OnPocketsChanged;
            }
            if (health != null) health.Damaged += OnHurt;
            if (life != null) life.RespawnStarted += OnRespawnStarted;
            ApplyUpgrades();
        }

        private void OnDisable()
        {
            actions?.FindActionMap("Player").Disable();
            if (pockets != null)
            {
                pockets.Purchased -= OnPurchased;
                pockets.Changed -= OnPocketsChanged;
            }
            if (health != null) health.Damaged -= OnHurt;
            if (life != null) life.RespawnStarted -= OnRespawnStarted;
            ShowPlayer();
        }

        private void OnDestroy()
        {
            if (actions != null) Destroy(actions);
            if (Instance == this) Instance = null;
        }

        private bool wasUnlocked;

        private void OnPocketsChanged()
        {
            ApplyUpgrades();
            if (!wasUnlocked && Unlocked)
            {
                wasUnlocked = true;
                MudHud.Ensure();
                PermitUnlocked?.Invoke(this);
            }
        }

        private void OnPurchased(Upgrade upgrade) => ApplyUpgrades();

        /// <summary>Re-reads every owned upgrade (from the base numbers, so nothing stacks twice).</summary>
        public void ApplyUpgrades()
        {
            float chug = BaseChugSeconds, cooldown = BaseCooldown, radius = BaseRadius, damage = 1f, stun = BaseStun, bumper = 1f;
            int drinks = BaseChugDrinks;
            bool reinforced = false, tires = false, spill = false, waders = false;
            if (pockets != null)
                foreach (string id in pockets.Upgrades)
                {
                    Upgrade upgrade = UpgradeCatalog.Find(id);
                    if (upgrade == null) continue;
                    chug += upgrade.ChugSeconds;
                    cooldown += upgrade.CarCooldown;
                    radius += upgrade.CarBlastRadius;
                    damage += upgrade.CarBlastDamage;
                    stun += upgrade.CarStun;
                    bumper += upgrade.CarRunOverDamage;
                    drinks += upgrade.ChugDrinks;
                    reinforced |= upgrade.CarReinforced;
                    tires |= upgrade.MudTires;
                    spill |= upgrade.SpillProof;
                    waders |= upgrade.Waders;
                }
            ChugSeconds = Mathf.Max(0.4f, chug);
            CooldownLength = Mathf.Max(5f, cooldown);
            BlastRadius = radius;
            BlastDamage = BaseDamage * damage;
            StunSeconds = stun;
            BumperMultiplier = bumper;
            ChugDrinks = Mathf.Max(0, drinks);
            Reinforced = reinforced;
            MudTires = tires;
            SpillProof = spill;
            if (slow != null) slow.Waders = waders;
        }

        // ---------- Blocking (cutscenes, menus, the camp) ----------

        /// <summary>Stops new rides while <paramref name="key"/> holds it (cutscenes, conversations).</summary>
        public void Block(object key) => blockers.Add(key);
        public void Unblock(object key) => blockers.Remove(key);

        /// <summary>
        /// Ends a ride or chug at once. With <paramref name="explode"/> false the car is parked in a puff of smoke
        /// (a cutscene starting mid-ride) and no cooldown is charged.
        /// </summary>
        public void ForceEnd(bool explode)
        {
            if (Current == State.Chugging) AbortChug(false);
            if (car != null && !car.Ended) car.End(explode ? DrunkCar.EndReason.Bailed : DrunkCar.EndReason.Parked, null);
        }

        private void OnRespawnStarted(string _) => ForceEnd(false);

        // ---------- Update ----------

        private void Update()
        {
            if (drink == null) return;
            // The walking controller is switched off in the car, and switching it off frees the mouse; so while
            // driving (and no menu is open) this keeps the mouse captured itself.
            if (Current == State.Driving && Application.isFocused && !UiMenus.AnyOpen && !UiMenus.ChangedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            bool controllable = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
            switch (Current)
            {
                case State.Ready: UpdateReady(controllable); break;
                case State.Chugging: UpdateChug(); break;
                case State.Driving: UpdateDriving(controllable); break;
            }
        }

        private void UpdateReady(bool controllable)
        {
            if (!(Unlocked || FreeRides) || !controllable) { holdStarted = -1f; return; } // the boss chase lends you a car even without a permit
            if (drink.WasPressedThisFrame())
            {
                holdStarted = Time.time;
                holdConsumed = false;
            }
            if (!drink.IsPressed()) { holdStarted = -1f; return; }
            if (holdStarted < 0f || holdConsumed || Time.time - holdStarted < HoldToChug) return;
            holdConsumed = true;
            if (!CanStart(out string reason))
            {
                Refuse(reason);
                return;
            }
            if (flask != null && flask.IsDrinking && !flask.CancelSip()) { Refuse("Finish your sip first."); return; }
            BeginChug();
        }

        /// <summary>Why a ride can't start right now (null if it can).</summary>
        public bool CanStart(out string reason)
        {
            reason = null;
            if (!Unlocked && !Free) reason = "You need a Learner's Permit.";
            else if (blockers.Count > 0 || UiMenus.AnyOpen) reason = "Not now.";
            else if (life != null && (life.IsRespawning || life.IsDead)) reason = "Not now.";
            else if (movement != null && (!movement.enabled || movement.IsStunned)) reason = "Not now.";
            else if (!Free && CooldownRemaining > 0f) reason = $"The car's not back yet ({CooldownRemaining:0} s).";
            else if (!Free && flask != null && flask.DrunkStacks + ChugDrinks > flask.BlackoutAfterStacks)
                reason = "Too drunk to drive. Wait it off.";
            else if (NoParkingZone.Contains(transform.position)) reason = NoParkingZone.ReasonAt(transform.position);
            else if (!FindParkingSpot(out _, out _)) reason = "No room to park here.";
            return reason == null;
        }

        private void Refuse(string reason)
        {
            if (Time.time - lastRefusal < 1f) return;
            lastRefusal = Time.time;
            MudHud.Toast(reason, new Color(1f, 0.75f, 0.4f));
            Sfx.Play("laser_fizzle", 0.4f, 1.3f);
        }

        // ---------- The chug ----------

        private void BeginChug()
        {
            Current = State.Chugging;
            chugStarted = Time.time;
            ChugRaise = 0f;
            ChugGulps = 0f;
            gear?.Holster(this);
            Sfx.Play("flask_uncork", 0.8f, 0.85f);
            ChugStarted?.Invoke();
        }

        private float nextGlug;

        private void UpdateChug()
        {
            float t = ChugProgress;
            ChugRaise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.22f));
            ChugGulps = Mathf.Clamp01((t - 0.22f) / 0.66f) * 3f;
            if (slow != null) slow.ExtraSlow = 0.5f;
            if (flask != null) flask.ExtraDaze = Mathf.Max(flask.ExtraDaze, Mathf.Clamp01(t * 1.2f) * 0.6f);
            if (t > 0.22f && t < 0.9f && Time.time >= nextGlug)
            {
                nextGlug = Time.time + ChugSeconds * 0.22f;
                Sfx.Play("flask_glug", 0.8f, Random.Range(0.8f, 0.95f));
            }
            if (t < 1f) return;

            // Flask drained. Call the car.
            if (!FindParkingSpot(out Vector3 at, out Quaternion facing))
            {
                AbortChug(false);
                Refuse("No room to park here.");
                return;
            }
            if (!Free && flask != null) flask.AddDrinks(ChugDrinks);
            Sfx.Play("flask_ahh", 0.9f, 0.9f);
            if (slow != null) slow.ExtraSlow = 0f;
            StartCoroutine(FadeDaze());
            SpawnCar(at, facing);
        }

        private void AbortChug(bool spilled)
        {
            Current = State.Ready;
            freeRide = false;
            ChugRaise = 0f;
            if (slow != null) slow.ExtraSlow = 0f;
            gear?.Unholster(this);
            StartCoroutine(FadeDaze());
            if (!spilled) return;
            cooldownUntil = Mathf.Max(cooldownUntil, Time.time + 3f);
            MudHud.Toast("Spilled it!", new Color(1f, 0.55f, 0.35f));
            Sfx.Play("flask_glug", 0.7f, 1.6f);
        }

        private IEnumerator FadeDaze()
        {
            if (flask == null) yield break;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
            {
                flask.ExtraDaze = Mathf.Min(flask.ExtraDaze, 0.6f * (1f - t));
                yield return null;
            }
            flask.ExtraDaze = 0f;
        }

        private void OnHurt(Health _, float amount, GameObject source)
        {
            if (Current == State.Chugging && !SpillProof && !Free && amount >= SpillDamage) AbortChug(true);
        }

        // ---------- Parking and the car ----------

        /// <summary>Somewhere the car fits: where the player stands, or a little ahead or to the side.</summary>
        private bool FindParkingSpot(out Vector3 position, out Quaternion facing)
        {
            float yaw = transform.eulerAngles.y;
            facing = Quaternion.Euler(0f, yaw, 0f);
            Vector3[] offsets = { Vector3.zero, facing * new Vector3(0f, 0f, 2.5f), facing * new Vector3(2.5f, 0f, 0f), facing * new Vector3(-2.5f, 0f, 0f), facing * new Vector3(0f, 0f, -2.5f) };
            foreach (Vector3 offset in offsets)
            {
                Vector3 probe = transform.position + offset;
                if (!Physics.Raycast(probe + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 8f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (ground.normal.y < 0.75f) continue;
                Vector3 spot = ground.point;
                if (MudZone.At(spot).kind == MudZone.Kind.BogWater && MudZone.At(spot).strength > 0.5f) continue;
                Vector3 centre = spot + Vector3.up * 1.25f;
                bool blocked = false;
                foreach (Collider hit in Physics.OverlapBox(centre, new Vector3(1.0f, 0.55f, 2.2f), facing, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(transform) || hit is CharacterController) continue;
                    if (hit.GetComponentInParent<Health>() != null) continue; // creatures get bumped out of the way
                    blocked = true;
                    break;
                }
                if (blocked) continue;
                position = spot;
                return true;
            }
            position = transform.position;
            return false;
        }

        private void SpawnCar(Vector3 at, Quaternion facing)
        {
            car = DrunkCar.Create(this, at + Vector3.up * 4.5f, facing);
            if (Free)
            {
                car.TopSpeed = 26f;
                car.EndlessBottle = true;
            }
            ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, at + Vector3.up * 1.2f, Vector3.up, 45, 1.4f, 3f, 85f, new Color(0.85f, 0.85f, 0.85f, 0.8f));
            Sfx.Play("rev", 0.8f, 1f);
            EnterCar();
            Current = State.Driving;
            CarSpawned?.Invoke(car);
        }

        private void EnterCar()
        {
            if (movement != null) movement.enabled = false;
            if (body != null) body.enabled = false;
            if (flask != null) flask.enabled = false;
            if (interactor != null) interactor.enabled = false;
            gear?.Holster(this);
            health.Invulnerable = true;
            health.Blocked += OnBlocked;
            transform.SetParent(car.Seat, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            // The walking player (hands, tools, anything on the rig) rides along at the seat but must not poke out
            // through the roof or doors in the chase camera's view: hide it until the ride ends.
            hidden.Clear();
            foreach (Renderer part in GetComponentsInChildren<Renderer>())
                if (part.enabled) { part.enabled = false; hidden.Add(part); }
            if (view != null)
            {
                chase = ChaseCamera.Create(view, car);
                view.enabled = false;
            }
        }

        private void OnBlocked(Health _, float amount, GameObject source)
        {
            if (car != null) car.TakeHit(amount);
        }

        private float bailHeld;

        private void UpdateDriving(bool controllable)
        {
            if (car == null) return;
            Vector2 axis = controllable && !UiMenus.AnyOpen ? move.ReadValue<Vector2>() : Vector2.zero;
            car.SetInput(axis.y, axis.x, controllable && jump.IsPressed());
            if (!controllable) return;
            if (attack.WasPressedThisFrame()) car.Honk();
            // F bails out (blows it up where it is). A short press is enough; the first frames after the chug are ignored.
            if (drink.WasPressedThisFrame() && Time.time - chugStarted > ChugSeconds + 0.6f)
                car.End(DrunkCar.EndReason.Bailed, null);
        }

        /// <summary>The car is about to explode; the driver is still inside, safe.</summary>
        internal void OnCarExploding(DrunkCar ending) { }

        /// <summary>Called by the car once it has ended (exploded or parked).</summary>
        internal void OnCarEnded(DrunkCar ending, DrunkCar.EndReason reason, Vector3 where, Vector3 travel)
        {
            if (ending != car) return;
            car = null;
            health.Blocked -= OnBlocked;
            transform.SetParent(null, true);
            ShowPlayer();
            if (chase != null) chase.Release();
            chase = null;
            if (view != null) view.enabled = true;
            // A free car (the boss chase) costs no cooldown, even when the chase has just ended under it.
            if (!Free && !ending.EndlessBottle && reason != DrunkCar.EndReason.Parked) cooldownUntil = Time.time + CooldownLength;
            freeRide = false;
            RideEnded?.Invoke(ending, reason, where);
            if (reason == DrunkCar.EndReason.Parked)
            {
                // Parked for a cutscene: back on foot at once, and not left invulnerable for the scene to inherit.
                Land(SafeGround(where, travel), false);
                health.Invulnerable = false;
                return;
            }
            string message = reason switch
            {
                DrunkCar.EndReason.BottleEmpty => "Flask's dry!",
                DrunkCar.EndReason.Wrecked => "Wrecked!",
                DrunkCar.EndReason.Flipped => "Flipped it!",
                DrunkCar.EndReason.Stalled => "Stalled out!",
                DrunkCar.EndReason.Bailed => "Bail!",
                _ => null
            };
            if (message != null) MudHud.Toast(message, new Color(1f, 0.6f, 0.3f));
            StartCoroutine(Tumble(where, travel));
        }

        private void ShowPlayer()
        {
            foreach (Renderer part in hidden) if (part != null) part.enabled = true;
            hidden.Clear();
        }

        // Thrown clear in a comic tumble: up, over and down onto solid ground, unhurt.
        private IEnumerator Tumble(Vector3 from, Vector3 travel)
        {
            Current = State.Tumbling;
            Vector3 to = SafeGround(from, travel);
            Transform cam = view != null ? view.transform.parent : null; // the pivot
            Quaternion yaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            transform.rotation = yaw;
            const float seconds = 0.95f;
            Vector3 start = from + Vector3.up * 0.6f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                Vector3 p = Vector3.Lerp(start, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 5f;
                transform.position = p;
                if (cam != null) cam.localRotation = Quaternion.Euler(t * 360f * 1.25f, 0f, Mathf.Sin(t * Mathf.PI) * 25f);
                yield return null;
            }
            Land(to, true);
        }

        private void Land(Vector3 at, bool dizzy)
        {
            transform.position = at;
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            if (body != null) body.enabled = true;
            if (movement != null)
            {
                movement.ResetMotion();
                movement.enabled = true;
                if (dizzy) movement.Stun(0.5f);
            }
            if (flask != null) flask.enabled = true;
            if (interactor != null) interactor.enabled = true;
            gear?.Unholster(this);
            if (dizzy)
            {
                Sfx.Play("player_land", 0.9f, 0.8f);
                ParticleBurst.Spawn(ParticleBurst.Kind.Sand, at, Vector3.up, 20, 0.6f, 3f, 80f, new Color(0.4f, 0.32f, 0.22f, 0.8f));
                CameraShake.Add(0.3f);
            }
            Current = State.Ready;
            if (dizzy) StartCoroutine(GraceThenVulnerable(0.5f));
        }

        private IEnumerator GraceThenVulnerable(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (Current != State.Driving) health.Invulnerable = false;
        }

        /// <summary>Solid, walkable ground near a point, preferring behind the car's travel (thrown clear).</summary>
        private Vector3 SafeGround(Vector3 near, Vector3 travel)
        {
            travel.y = 0f;
            Vector3 back = travel.sqrMagnitude > 1f ? -travel.normalized : -transform.forward;
            foreach (float distance in new[] { 3f, 5f, 1.5f, 0f })
            {
                Vector3 probe = near + back * distance;
                if (NavMesh.SamplePosition(probe, out NavMeshHit hit, 4f, NavMesh.AllAreas) && MudZone.At(hit.position).kind != MudZone.Kind.BogWater)
                    return hit.position + Vector3.up * 0.1f;
            }
            if (NavMesh.SamplePosition(near, out NavMeshHit any, 25f, NavMesh.AllAreas)) return any.position + Vector3.up * 0.1f;
            if (Physics.Raycast(near + Vector3.up * 20f, Vector3.down, out RaycastHit ground, 60f, ~0, QueryTriggerInteraction.Ignore))
                return ground.point + Vector3.up * 0.1f;
            return near + Vector3.up;
        }

        /// <summary>A free car for the story (the boss chase): starts a chug if the player is ready.</summary>
        public bool TryStartFree()
        {
            if (Current != State.Ready) return false;
            freeRide = true;
            if (!CanStart(out string reason)) { freeRide = false; Refuse(reason); return false; }
            if (flask != null && flask.IsDrinking) flask.CancelSip();
            BeginChug();
            return true;
        }
    }
}
