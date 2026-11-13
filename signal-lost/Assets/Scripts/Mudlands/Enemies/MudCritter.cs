using System;
using System.Collections.Generic;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies.Desert;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>Prefab numbers for a creature, read by the Mudlands builder.</summary>
    public readonly struct CritterSpec
    {
        public readonly string Name;
        public readonly float Health, Radius, Height, Speed, Territory;
        public readonly (Inventory.ScrapType type, int min, int max)[] Drops;

        public CritterSpec(string name, float health, float radius, float height, float speed, float territory,
            params (Inventory.ScrapType, int, int)[] drops)
        {
            Name = name;
            Health = health;
            Radius = radius;
            Height = height;
            Speed = speed;
            Territory = territory;
            Drops = drops;
        }
    }

    /// <summary>
    /// Shared base for the Mudlands creatures (docs/mudlands.md). It sits on <see cref="DesertFodder"/>, so the
    /// fighting radius, stun rule, hit flash, spawners and no-friendly-fire rule are the same as every other enemy;
    /// this adds what makes the Mudlands' creatures feel alive:
    /// <list type="bullet">
    /// <item>a code-built model (<see cref="BuildModel"/>), made in Awake if the prefab's Visual is empty;</item>
    /// <item>reactions: a "!" and startle hop when they first notice you, cheering when you get hurt near them,
    /// gasping or mourning when an ally dies close by, panic at a horn or an oncoming car;</item>
    /// <item>squash and stretch, a head that watches you, blinking;</item>
    /// <item>being launched (run over, blasted): a ballistic tumble, then landing dazed (or dead);</item>
    /// <item>a buff hook the Banjo Toad uses (<see cref="Tempo"/>).</item>
    /// </list>
    /// Each creature only writes its own states and model.
    /// </summary>
    public abstract class MudCritter : DesertFodder, IVehicleReactive
    {
        private static readonly List<MudCritter> All = new List<MudCritter>();

        [Header("Personality")]
        [Tooltip("How high over the origin its emotes pop up.")]
        [SerializeField, Min(0.2f)] protected float emoteHeight = 1.6f;
        [Tooltip("Shown in the HUD's target readout and kept in the scene name.")]
        [SerializeField] protected string displayName = "";

        protected Transform Head { get; set; }
        protected Transform EyeL { get; set; }
        protected Transform EyeR { get; set; }
        protected bool Spotted { get; private set; }
        /// <summary>A random number per creature (0-10), so a crowd's idles don't move in step.</summary>
        protected float Seed { get; private set; }

        /// <summary>Banjo Toad's tune: allies move this much faster (1 = normal).</summary>
        public float Tempo { get; private set; } = 1f;
        private float tempoUntil;

        private float squash, squashVelocity, nextCarCheck, nextBlink, blinkUntil, nextIdleEmote;
        private bool airborne;
        private Vector3 flightVelocity, flightSpin;
        private float flightLanding;
        private Vector3 visualScale = Vector3.one;

        public static event Action<MudCritter> CritterDied;
        public static IReadOnlyList<MudCritter> Living => All;
        public bool Airborne => airborne;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            All.Clear();
            CritterDied = null;
        }

        // ---------- Setup ----------

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
            Seed = Random.value * 10f;
            corpseTime = Mathf.Max(corpseTime, 3.2f); // time to land and play its own death
            visualScale = Visual.localScale;
            if (!string.IsNullOrEmpty(displayName)) name = displayName;
        }

        /// <summary>Builds the creature's look under <paramref name="visual"/> (facing +Z, feet at y = 0).</summary>
        protected abstract void BuildModel(Transform visual);

        protected override void OnEnable()
        {
            base.OnEnable();
            All.Add(this);
            CritterDied += OnAnyCritterDied;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            All.Remove(this);
            CritterDied -= OnAnyCritterDied;
            if (TargetHealth != null) TargetHealth.Damaged -= OnPlayerDamaged;
        }

        protected override void OnStarted()
        {
            if (TargetHealth != null) TargetHealth.Damaged += OnPlayerDamaged;
            nextBlink = Time.time + Random.Range(1f, 4f);
            nextIdleEmote = Time.time + Random.Range(6f, 14f);
            OnCritterStarted();
        }

        protected virtual void OnCritterStarted() { }

        // ---------- The frame ----------

        protected sealed override void Tick(float dt)
        {
            if (airborne) return; // flying through the air (LateUpdate moves it)
            if (Time.time >= tempoUntil) Tempo = 1f;
            if (!Spotted && Notices())
            {
                Spotted = true;
                OnSpotted();
            }
            else if (Spotted && !Notices() && ReturningHome) Spotted = false;
            CheckCar();
            Think(dt);
        }

        /// <summary>The creature's own state machine.</summary>
        protected abstract void Think(float dt);

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (airborne && Started) UpdateFlight(Time.deltaTime);
            if (Started && !IsDead) Animate(Time.deltaTime);
            if (IsDead && !airborne) return;
            // Squash and stretch: a spring back to rest.
            squashVelocity += (-squash * 160f - squashVelocity * 14f) * Time.deltaTime;
            squash += squashVelocity * Time.deltaTime;
            float s = Mathf.Clamp(squash, -0.45f, 0.45f);
            Visual.localScale = Vector3.Scale(visualScale, new Vector3(1f + s * 0.5f, 1f - s, 1f + s * 0.5f));
            Blink();
        }

        /// <summary>The creature's procedural animation, every frame while alive (after the state machine).</summary>
        protected virtual void Animate(float dt) { }

        /// <summary>Squashes (positive) or stretches (negative) the body; it springs back.</summary>
        protected void Squash(float amount) => squash = Mathf.Clamp(squash + amount, -0.5f, 0.5f);

        private void Blink()
        {
            if (EyeL == null || EyeR == null) return;
            if (Time.time >= nextBlink)
            {
                blinkUntil = Time.time + 0.12f;
                nextBlink = Time.time + Random.Range(2f, 5f);
            }
            float eye = Time.time < blinkUntil ? 0.1f : 1f;
            EyeL.localScale = new Vector3(1f, eye, 1f);
            EyeR.localScale = new Vector3(1f, eye, 1f);
        }

        /// <summary>Turns the head toward the player (limited), on top of whatever the body is doing.</summary>
        protected void WatchPlayer(float limit = 50f, float speed = 6f)
        {
            if (Head == null || Target == null) return;
            Vector3 local = transform.InverseTransformPoint(TargetView != null ? TargetView.position : Target.position + Vector3.up * 1.5f) - Head.localPosition;
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -limit, limit);
            float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -25f, 25f);
            Head.localRotation = Quaternion.Slerp(Head.localRotation, Quaternion.Euler(pitch, yaw, 0f), Time.deltaTime * speed);
        }

        // ---------- Reactions ----------

        protected Emote Say(string symbol, Color color, float seconds = 1.1f, float size = 4f) =>
            Emote.Show(transform, symbol, color, emoteHeight, seconds, size);

        /// <summary>First noticed the player: by default a "!" and a startle hop.</summary>
        protected virtual void OnSpotted()
        {
            Say("!", new Color(1f, 0.85f, 0.3f));
            Squash(-0.35f);
        }

        /// <summary>Another Mudlands creature died within 12 m. Default: a gasp.</summary>
        protected virtual void OnAllyDied(MudCritter ally) => Say("!?", new Color(0.8f, 0.9f, 1f));

        /// <summary>The player took a hit within 15 m. Default: a cheer and a hop.</summary>
        protected virtual void OnPlayerHurt()
        {
            Say(Random.value < 0.5f ? "HA!" : "~hum~", new Color(1f, 0.6f, 0.9f), 0.9f, 3f);
            Squash(-0.25f);
        }

        private void OnAnyCritterDied(MudCritter ally)
        {
            if (ally == this || IsDead || ally == null) return;
            if ((ally.transform.position - transform.position).sqrMagnitude > 144f) return;
            OnAllyDied(ally);
        }

        private float lastCheer;

        private void OnPlayerDamaged(Health _, float amount, GameObject source)
        {
            if (IsDead || !Started || Time.time - lastCheer < 2.5f || Target == null) return;
            if (FlatDistance(Target.position) > 15f || Random.value > 0.6f) return;
            lastCheer = Time.time;
            OnPlayerHurt();
        }

        // ---------- Cars ----------

        private void CheckCar()
        {
            if (Time.time < nextCarCheck) return;
            nextCarCheck = Time.time + 0.2f + Random.value * 0.1f;
            DrunkCar car = DrunkDriving.Instance != null ? DrunkDriving.Instance.Car : null;
            if (car == null || car.Ended) return;
            Vector3 toMe = transform.position - car.transform.position;
            toMe.y = 0f;
            float distance = toMe.magnitude;
            if (distance > 22f) return;
            Vector3 velocity = car.Velocity;
            velocity.y = 0f;
            float closing = Vector3.Dot(velocity, toMe.normalized);
            if (closing < 4f) return;
            // Only if the car is actually coming its way (within a few metres of its path).
            float miss = Vector3.Cross(velocity.normalized, toMe).magnitude;
            if (miss > 4f) return;
            OnCarApproaching(car, distance / closing);
        }

        public virtual VehicleResponse OnVehicleHit(VehicleHit hit)
        {
            if (hit.Speed < 6f) return VehicleResponse.Bounce;
            RunOver(hit, 0.6f);
            return VehicleResponse.RunOver;
        }

        public virtual void OnCarApproaching(DrunkCar car, float secondsAway) { }

        /// <summary>The default run-over: damage, a "!!", and a high spinning launch along the car's path.</summary>
        protected void RunOver(VehicleHit hit, float lift)
        {
            Say("!!", new Color(1f, 0.4f, 0.3f), 0.8f);
            Health.TakeDamage(hit.Damage, hit.Car != null && hit.Car.Driver != null ? hit.Car.Driver.gameObject : null);
            Launch(hit.Direction * (hit.Speed * 0.55f) + Vector3.up * (6f + hit.Speed * lift * 0.35f), 720f);
        }

        /// <summary>Throws it through the air (it tumbles, lands, then carries on, or finishes dying).</summary>
        public void Launch(Vector3 velocity, float spinDegrees)
        {
            if (airborne) return;
            airborne = true;
            flightVelocity = velocity;
            flightSpin = Random.onUnitSphere * spinDegrees;
            flightLanding = 0f;
            if (Agent != null)
            {
                if (AgentReady) Agent.isStopped = true;
                Agent.enabled = false;
            }
            OnLaunched();
        }

        protected virtual void OnLaunched() { }
        protected virtual void OnLanded() { }

        private void UpdateFlight(float dt)
        {
            flightVelocity += Physics.gravity * dt;
            Vector3 next = transform.position + flightVelocity * dt;
            Visual.Rotate(flightSpin * dt, Space.World);
            bool falling = flightVelocity.y < 0f;
            if (falling && Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit ground,
                    0.5f - flightVelocity.y * dt + 0.05f, sightMask, QueryTriggerInteraction.Ignore) && !ground.collider.transform.IsChildOf(transform) &&
                ground.collider.GetComponentInParent<DrunkCar>() == null)
            {
                transform.position = ground.point;
                airborne = false;
                Visual.localRotation = VisualRestRotation;
                Squash(0.45f);
                ParticleBurst.Spawn(ParticleBurst.Kind.Sand, ground.point, Vector3.up, 14, 0.5f, 3f, 70f, new Color(0.35f, 0.27f, 0.2f, 0.8f));
                Sfx.PlayAt("mud_splat", ground.point, 0.6f, Random.Range(0.9f, 1.2f), 25f);
                if (!IsDead && Agent != null)
                {
                    AgentUtil.Reenable(Agent, 5f);
                    Stun(0.9f);
                }
                OnLanded();
                return;
            }
            transform.position = next;
            flightLanding += dt;
            if (flightLanding > 6f) { airborne = false; if (!IsDead) Kill(); } // fell off the world
        }

        private float landedDeadAt = -1f;

        protected override void UpdateDeath(float t)
        {
            if (airborne) return;
            if (landedDeadAt < 0f) landedDeadAt = Time.time;
            DeathPose(Time.time - landedDeadAt);
        }

        /// <summary>The creature's own death animation; t is seconds since it died (and landed, if launched).</summary>
        protected virtual void DeathPose(float t) => base.UpdateDeath(t);

        protected override void OnDeath()
        {
            CritterDied?.Invoke(this);
            OnCritterDeath();
        }

        protected virtual void OnCritterDeath() { }

        // ---------- Buffs ----------

        /// <summary>Banjo Toad's tune: faster for a moment.</summary>
        public void Buff(float tempo, float seconds)
        {
            Tempo = Mathf.Max(Tempo, tempo);
            tempoUntil = Mathf.Max(tempoUntil, Time.time + seconds);
        }

        // ---------- Helpers ----------

        /// <summary>Moves toward a point at a speed (scaled by the tune and the meerkat alert).</summary>
        protected void MoveTo(Vector3 point, float speed) => Chase(point, speed * Tempo);

        /// <summary>Hurts the player if they are within <paramref name="range"/> and <paramref name="arc"/> degrees of facing.</summary>
        protected bool Strike(float range, float arc, float damage, float shove = 0f)
        {
            if (!TargetAlive) return false;
            Vector3 to = Target.position - transform.position;
            to.y = 0f;
            if (to.magnitude > range || Vector3.Angle(transform.forward, to) > arc * 0.5f) return false;
            bool landed = TargetHealth != null && TargetHealth.TakeDamage(damage, gameObject);
            if (landed && shove > 0f && TargetBody != null && TargetBody.enabled) StartCoroutine(Shove(to.normalized, shove));
            return landed;
        }

        private System.Collections.IEnumerator Shove(Vector3 direction, float distance)
        {
            for (float moved = 0f; moved < distance && TargetBody != null && TargetBody.enabled;)
            {
                float step = Mathf.Min(distance - moved, 12f * Time.deltaTime);
                TargetBody.Move(direction * step);
                moved += step;
                yield return null;
            }
        }

        /// <summary>A random idle line every so often while nothing is happening.</summary>
        protected bool IdleEmoteDue()
        {
            if (Time.time < nextIdleEmote) return false;
            nextIdleEmote = Time.time + Random.Range(7f, 15f);
            return true;
        }

        protected float PlayerDistance => Target != null ? FlatDistance(Target.position) : float.MaxValue;
        protected Vector3 PlayerPosition => Target != null ? Target.position : transform.position;
    }
}
