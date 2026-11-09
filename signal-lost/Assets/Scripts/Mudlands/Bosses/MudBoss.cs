using System;
using System.Collections;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies.Desert;
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
    /// Shared base for the Mudlands' mini-bosses and stage boss. It builds on <see cref="DesertMiniBoss"/> (patrol,
    /// engage, the boss bar, phases, leash and reset, the ship-part drop) and adds:
    /// <list type="bullet">
    /// <item>a code-built model and rig (<see cref="BuildModel"/>);</item>
    /// <item>a cutscene entrance (<see cref="Entrance"/>), played by <see cref="MudBossEncounter"/> with a
    /// <see cref="BossEntrance"/> camera;</item>
    /// <item>answers to the Drunk Driving car: a head-on car always blows up (<see cref="OnVehicleHit"/>), and each boss
    /// decides what the blast does to it (<see cref="OnCarBlast"/>);</item>
    /// <item>a key item dropped beside its ship part (an upgrade recipe ingredient).</item>
    /// </list>
    /// </summary>
    public abstract class MudBoss : DesertMiniBoss, ICarBlastReactive, IVehicleReactive
    {
        [Header("Mudlands")]
        [Tooltip("A key item it drops (an upgrade ingredient), or empty.")]
        [SerializeField] protected string keyItemDrop = "";
        [Tooltip("Scrap in its loot pile.")]
        [SerializeField, Min(0)] protected int scrapDrop = 12;

        protected float BossSeed { get; private set; }
        protected override string PartStage => MudlandsProgress.Stage;

        /// <summary>Raised when the boss is beaten, after its loot drops.</summary>
        public event Action<MudBoss> Beaten;

        protected override void Awake()
        {
            Transform model = transform.Find("Visual");
            if (model == null)
            {
                model = new GameObject("Visual").transform;
                model.SetParent(transform, false);
            }
            if (model.GetComponentInChildren<Renderer>() == null) BuildModel(model);
            base.Awake();
            BossSeed = Random.value * 10f;
            if (FaceParts.HasValue)
            {
                BossFace.Rig rig = FaceParts.Value;
                if (rig.Body == null) rig.Body = model;
                Face = BossFace.Attach(this, rig);
            }
        }

        // A flier's fight starts in the air, but the shared start-up looks for the NavMesh right under the body:
        // stand it on the ground for that one call and put it back, all inside one frame.
        protected override void Start()
        {
            if (WalksAfterEntrance) { base.Start(); return; }
            Vector3 air = transform.position;
            transform.position = Home;
            base.Start();
            if (Agent != null) Agent.enabled = false;
            transform.position = air;
        }

        // ---------- The face ----------

        /// <summary>Set by <see cref="BuildModel"/>: the brows, eyes and mouth the face animates.</summary>
        protected BossFace.Rig? FaceParts { get; set; }
        /// <summary>The boss's expressions (null for a boss without a face).</summary>
        protected BossFace Face { get; private set; }
        /// <summary>The camera of the boss's entrance while it plays (its eyes follow it).</summary>
        public Camera FaceCamera { get; set; }

        /// <summary>Where the boss's eyes look: the entrance camera, else the player in a fight, else straight ahead.</summary>
        public Vector3? FaceLookPoint
        {
            get
            {
                if (FaceCamera != null) return FaceCamera.transform.position;
                if (Target != null && Engaged) return Target.position + Vector3.up * 1.5f;
                return null;
            }
        }

        /// <summary>Where the fight starts after the entrance (the encounter puts the boss here if it is far off).</summary>
        public virtual Vector3 FightStart(Vector3 home) => home;
        /// <summary>False for a boss that doesn't walk on the NavMesh when its fight starts (it flies).</summary>
        public virtual bool WalksAfterEntrance => true;

        /// <summary>Builds the boss's model under <paramref name="visual"/> (facing +Z, feet at y = 0), and its ship part.</summary>
        protected abstract void BuildModel(Transform visual);

        /// <summary>The cutscene entrance. Runs while the boss's own script is still switched off.</summary>
        public abstract IEnumerator Entrance(BossEntrance cine);

        /// <summary>Where the cutscene starts its camera, given the player's side of the arena.</summary>
        public virtual string Title => DisplayName;
        public abstract string Subtitle { get; }

        // ---------- The car ----------

        public virtual VehicleResponse OnVehicleHit(VehicleHit hit) => VehicleResponse.Explode;
        public virtual void OnCarApproaching(DrunkCar car, float secondsAway) { }
        public virtual float OnCarBlast(CarBlastInfo blast) => blast.Damage;

        // ---------- Loot ----------

        protected override void OnBossDeath()
        {
            Vector3 at = transform.position;
            if (!string.IsNullOrEmpty(keyItemDrop))
                KeyItemPickup.Create(keyItemDrop, DropSpot(at + transform.right * 2f), KeyItemModel());
            for (int i = 0; i < scrapDrop; i++)
            {
                ScrapType type = i % 4 == 0 ? ScrapType.Wiring : i % 5 == 0 ? ScrapType.CircuitBoards : i % 7 == 0 ? ScrapType.FuelCells : ScrapType.ScrapMetal;
                Vector2 spread = Random.insideUnitCircle * 2.5f;
                ScrapPickup.Spawn(type, at + Vector3.up * 1.5f, new Vector3(spread.x, Random.Range(4f, 6f), spread.y));
            }
            OnMudBossDeath();
            Beaten?.Invoke(this);
        }

        protected virtual void OnMudBossDeath() { }

        /// <summary>The key item's look on the ground (defaults to a glowing bead).</summary>
        protected virtual Action<MeshKit> KeyItemModel() => k => k.Ball(Vector3.zero, 0.2f, new Color(1f, 0.8f, 0.4f), 1, 1.5f);

        // ---------- Helpers ----------

        protected Emote Say(string text, Color color, float seconds = 1.4f, float size = 5f, float height = 3f)
        {
            if (Face != null)
            {
                // Words get a moving mouth (a lone "!" or "..." doesn't), and the line its face.
                if (text.Length > 3) Face.Talk(Mathf.Min(seconds * 0.75f, 0.25f + text.Length * 0.06f));
                if (LineMoods.TryGetValue(text, out Mood mood)) Face.Show(mood, Mathf.Max(seconds, 0.8f));
            }
            return Emote.Show(transform, text, color, height, seconds, size);
        }

        // The face each boss line comes with.
        private static readonly System.Collections.Generic.Dictionary<string, Mood> LineMoods = new System.Collections.Generic.Dictionary<string, Mood>
        {
            ["!"] = Mood.Angry, ["?"] = Mood.Shock, ["!?"] = Mood.Shock, ["..."] = Mood.Focus, ["@_@"] = Mood.Sleepy,
            // Kart Kong
            ["BEEP BEEP!"] = Mood.Laugh, ["WHEEE!"] = Mood.Laugh, ["MY GO-KART!!!"] = Mood.Furious, ["WHOOPS!"] = Mood.Shock,
            ["*BURP*"] = Mood.Laugh, ["*munch*"] = Mood.Laugh, ["HKK!"] = Mood.Hurt, ["GRAAAH!"] = Mood.Furious, ["BONK!"] = Mood.Laugh,
            ["*clang*"] = Mood.Smug,
            // Master Silt
            ["You walk like a startled yak."] = Mood.Smug, ["HUP!"] = Mood.Furious, ["HAAA!"] = Mood.Furious, ["I HEAR YOU!"] = Mood.Furious,
            ["*huff*"] = Mood.Hurt, ["Now I am listening."] = Mood.Angry, ["...well fought."] = Mood.Neutral,
            // Granny Drowse
            ["*yaaawn*"] = Mood.Sleepy, ["HEHEHEHEHE!"] = Mood.Laugh, ["MY BROOM!"] = Mood.Shock, ["Stir, stir..."] = Mood.Smug,
            ["Oh, bother."] = Mood.Sleepy, ["Which one am I?"] = Mood.Laugh, ["OW! My concentration!"] = Mood.Hurt, ["WAAAH!"] = Mood.Shock,
            ["Up we go..."] = Mood.Sleepy, ["Now I'm AWAKE!"] = Mood.Furious,
            // Clutch McCheddar
            ["You're in my lane, pal."] = Mood.Smug, ["BOYS!"] = Mood.Laugh, ["WATCH THIS!"] = Mood.Smug, ["HOLD MY CHEESE!"] = Mood.Laugh,
            ["CRASH!"] = Mood.Hurt, ["OOF!"] = Mood.Hurt, ["Catch me if you can, cheese-for-brains!"] = Mood.Laugh,
            ["C'mon, slowpoke!"] = Mood.Smug, ["ROUND TWO!"] = Mood.Furious, ["Alright, no more Mr. Nice Mouse."] = Mood.Angry,
        };

        /// <summary>
        /// The ground under a point: the highest solid surface, skipping anything alive or breakable (this boss's own
        /// collider, creatures, earth walls, fences) and anything with a rigidbody (the car, debris, barrels), so a boss
        /// riding or landing never climbs onto itself or something passing by.
        /// </summary>
        protected static Vector3 Ground(Vector3 point) => GroundProbe.Under(point);

        /// <summary>Where a dropped item can sit and be reached: the NavMesh near the point (never a treetop or a roof).</summary>
        protected static Vector3 DropSpot(Vector3 point) =>
            NavMesh.SamplePosition(point, out NavMeshHit hit, 8f, NavMesh.AllAreas) ? hit.position : Ground(point);

        protected Vector3 PlayerPosition => Target != null ? Target.position : transform.position;
        protected float PlayerDistance => Target != null ? Flat(Target.position - transform.position) : float.MaxValue;

        /// <summary>A heavy hit from anything but the melee swing (a charged bolt, a jump smash, a blast).</summary>
        protected bool IsHeavyRanged(float amount, GameObject source) => amount >= HeavyHit && !IsMeleeHit(source);
        /// <summary>A full-charge bolt (95% of the laser's best).</summary>
        protected bool IsFullCharge(float amount, GameObject source) =>
            TargetLaser != null && !IsMeleeHit(source) && amount >= TargetLaser.MaxShotDamage * 0.95f;
        /// <summary>The combo's third hit (it stuns small things; bosses react in their own way).</summary>
        protected bool IsFinisher(GameObject source) =>
            IsMeleeHit(source) && TargetLaser.ComboLength > 1 && TargetLaser.ComboStep == TargetLaser.ComboLength;

        /// <summary>Hurts the player if they are inside a circle on the ground.</summary>
        protected bool HurtInCircle(Vector3 centre, float radius, float damage, float shove = 0f)
        {
            if (!TargetAlive) return false;
            Vector3 offset = PlayerPosition - centre;
            offset.y = 0f;
            if (offset.magnitude > radius) return false;
            if (!HurtPlayer(damage)) return false;
            if (shove > 0f) ShovePlayer(offset.sqrMagnitude > 0.01f ? offset : transform.forward, shove);
            return true;
        }

        protected void Dust(Vector3 at, int count = 25, float size = 0.7f)
        {
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, at, Vector3.up, count, size, 4f, 75f, new Color(0.38f, 0.3f, 0.21f, 0.8f));
        }
    }

    /// <summary>
    /// The camera for a boss's entrance: a separate camera (the player's is switched off), letterbox bars, the HUD
    /// out of the way, the player frozen and safe, the Drunk Driving car parked. Space skips. Bosses move the camera
    /// with <see cref="Cut"/>, <see cref="Move"/> and <see cref="LookAt"/> and wait with <see cref="Wait"/>.
    /// </summary>
    public sealed class BossEntrance
    {
        private readonly MonoBehaviour host;
        private Camera shot, view;
        private FirstPersonController movement;
        private PlayerLife player;
        private bool wasInvulnerable;
        private Transform lookTarget;
        private Vector3 lookOffset;

        public Camera Camera => shot;
        public bool Skipped { get; private set; }
        public Vector3 PlayerPosition => player != null ? player.transform.position : Vector3.zero;

        public BossEntrance(MonoBehaviour host) => this.host = host;

        public void Begin()
        {
            player = UnityEngine.Object.FindAnyObjectByType<PlayerLife>();
            DrunkDriving driving = DrunkDriving.Instance;
            if (driving != null)
            {
                driving.ForceEnd(false);
                driving.Block(this);
            }
            view = player != null ? player.GetComponentInChildren<Camera>() : Camera.main;
            movement = player != null ? player.GetComponent<FirstPersonController>() : null;
            if (player != null)
            {
                wasInvulnerable = player.Health.Invulnerable;
                player.Health.Invulnerable = true;
            }
            if (movement != null) movement.enabled = false;
            shot = new GameObject("Boss Entrance Camera").AddComponent<Camera>();
            if (view != null)
            {
                shot.CopyFrom(view);
                view.enabled = false;
            }
            shot.gameObject.tag = "MainCamera";
            shot.fieldOfView = 50f;
            UiMenus.Open(host, true);
            MudHud.Letterbox(true);
        }

        public void End()
        {
            if (shot != null) UnityEngine.Object.Destroy(shot.gameObject);
            if (view != null) view.enabled = true;
            UiMenus.Close(host);
            MudHud.Letterbox(false);
            if (movement != null)
            {
                movement.ResetMotion();
                movement.enabled = true;
            }
            if (player != null) player.Health.Invulnerable = wasInvulnerable;
            DrunkDriving.Instance?.Unblock(this);
        }

        /// <summary>Jumps the camera to a spot, looking at a point.</summary>
        public void Cut(Vector3 position, Vector3 look)
        {
            lookTarget = null;
            shot.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position));
        }

        /// <summary>Keeps the camera aimed at a moving thing (until the next Cut or Move).</summary>
        public void LookAt(Transform target, Vector3 offset)
        {
            lookTarget = target;
            lookOffset = offset;
        }

        /// <summary>Glides the camera from where it is to a spot over some seconds, aimed at a point (or the tracked target).</summary>
        public IEnumerator Move(Vector3 to, Vector3 look, float seconds, bool keepTracking = false)
        {
            Vector3 from = shot.transform.position;
            Quaternion fromRotation = shot.transform.rotation;
            if (!keepTracking) lookTarget = null;
            for (float t = 0f; t < seconds && !CheckSkip(); t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                shot.transform.position = Vector3.Lerp(from, to, k);
                Vector3 aim = lookTarget != null ? lookTarget.position + lookOffset : look;
                shot.transform.rotation = Quaternion.Slerp(fromRotation, Quaternion.LookRotation(aim - shot.transform.position), keepTracking ? 1f : k);
                yield return null;
            }
        }

        public IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds && !CheckSkip(); t += Time.deltaTime)
            {
                Track();
                yield return null;
            }
        }

        /// <summary>Call every frame inside custom loops: keeps any tracking going and notices Space.</summary>
        public bool Tick()
        {
            Track();
            return CheckSkip();
        }

        private void Track()
        {
            if (lookTarget != null && shot != null)
                shot.transform.rotation = Quaternion.Slerp(shot.transform.rotation,
                    Quaternion.LookRotation(lookTarget.position + lookOffset - shot.transform.position), Time.deltaTime * 6f);
        }

        public bool CheckSkip()
        {
            if (!Skipped && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) Skipped = true;
            return Skipped;
        }

        public void Skip() => Skipped = true;
    }

    /// <summary>A mini-boss's key item (an upgrade ingredient) floating on the ground: E to pick it up.</summary>
    public sealed class KeyItemPickup : MonoBehaviour, IInteractable
    {
        private string id;

        public static event Action<string> PickedUp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => PickedUp = null;

        public static KeyItemPickup Create(string itemId, Vector3 position, Action<MeshKit> model)
        {
            var root = new GameObject(KeyItems.Name(itemId));
            root.transform.position = position;
            Transform visual = MudArt.Part(root.transform, "Model", Vector3.zero, Quaternion.identity, "keyitem_" + itemId, model);
            root.AddComponent<FloatingPickup>().SetModel(visual);
            var glow = new GameObject("Glint").AddComponent<Light>();
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = Vector3.up;
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.8f, 0.4f);
            glow.range = 5f;
            glow.intensity = 3f;
            var pickup = root.AddComponent<KeyItemPickup>();
            pickup.id = itemId;
            return pickup;
        }

        public string Prompt => "Pick up " + KeyItems.Name(id);
        public bool CanInteract(PlayerInteractor player) => isActiveAndEnabled && player.Gear != null;

        public bool Interact(PlayerInteractor player)
        {
            PlayerInventory pockets = player.Gear.Inventory;
            if (pockets == null) return false;
            pockets.AddKeyItem(id);
            Sfx.Play("item_pickup", 0.9f);
            GameplayOverlay.Instance.ShowItemCard(KeyItems.Name(id),
                "A rare part. Eugene can build it into an upgrade at Bucket & Barter (Car filter).", new Color(1f, 0.8f, 0.4f), "KEY ITEM");
            PickedUp?.Invoke(id);
            Destroy(gameObject);
            return true;
        }
    }
}
