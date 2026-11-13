using System;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Player;
using UnityEngine;

namespace SignalLost.Melee
{
    /// <summary>
    /// Rage stacks (Adam, 11-10: a reason to clear the adds). Every enemy the player kills adds a stack, a melee kill
    /// two and a finisher three, up to <see cref="MaxStacks"/>. Each stack makes every hit the player deals harder
    /// (laser, bolts, fists, the car; bosses included) and adds run speed, and the knuckles' chain lightning and
    /// shockwaves reach further and jump more with it. After <see cref="HoldSeconds"/> without a kill the stacks drain
    /// one at a time. Shown by <see cref="RageHud"/>.
    ///
    /// Added to the player at runtime by <see cref="MeleeLoadout"/> (the final boss level and the melee arena only). Numbers are provisional (not agreed balance).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RageMeter : MonoBehaviour
    {
        public enum KillKind { Any, Melee, Finisher }

        public const int MaxStacks = 10;
        /// <summary>Extra damage per stack (10 stacks: +60%).</summary>
        public const float DamagePerStack = 0.06f;
        /// <summary>Extra run speed per stack (10 stacks: +30%).</summary>
        public const float SpeedPerStack = 0.03f;
        /// <summary>Seconds after the last kill before stacks start draining.</summary>
        public const float HoldSeconds = 6f;
        /// <summary>Seconds per stack lost while draining.</summary>
        public const float DrainSeconds = 1.2f;

        private static KillKind context = KillKind.Any;

        private FirstPersonController movement;
        private PlayerLife life;
        private float lastKill = float.NegativeInfinity, nextDrain;

        public static RageMeter Instance { get; private set; }

        public int Stacks { get; private set; }
        public float DamageMultiplier => 1f + DamagePerStack * Stacks;
        public float SpeedMultiplier => 1f + SpeedPerStack * Stacks;
        /// <summary>How much further chain lightning and shockwaves reach (10 stacks: +50%).</summary>
        public float ReachMultiplier => 1f + 0.05f * Stacks;
        /// <summary>Extra chain lightning jumps (one per three stacks).</summary>
        public int ExtraJumps => Stacks / 3;
        public bool Maxed => Stacks >= MaxStacks;
        /// <summary>0-1: how long until the stacks start draining (1 just after a kill).</summary>
        public float HoldLeft => Stacks == 0 ? 0f : Mathf.Clamp01(1f - (Time.time - lastKill) / HoldSeconds);
        /// <summary>Time.time of the last gain, for HUD flashes.</summary>
        public float LastGain { get; private set; } = float.NegativeInfinity;
        /// <summary>Raised with the new stack count and how many were gained (negative when draining).</summary>
        public event Action<int, int> Changed;

        /// <summary>
        /// Marks the kills dealt inside a using block as melee or finisher kills (Health raises its kill event while the
        /// damage is being dealt, so the kind is known when the kill is counted).
        /// </summary>
        public static IDisposable As(KillKind kind) => new Scope(kind);

        private readonly struct Scope : IDisposable
        {
            private readonly KillKind previous;
            public Scope(KillKind kind)
            {
                previous = context;
                context = kind;
            }
            public void Dispose() => context = previous;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            context = KillKind.Any;
            Instance = null;
        }

        private void Awake()
        {
            movement = GetComponent<FirstPersonController>();
            life = GetComponent<PlayerLife>();
        }

        private void OnEnable()
        {
            Instance = this;
            Health.AnyKilled += OnKilled;
            Health.OutgoingModifier = Boost;
            if (life != null) life.RespawnStarted += ResetStacks;
            RageHud.Ensure(this);
        }

        private void OnDisable()
        {
            Health.AnyKilled -= OnKilled;
            if (Health.OutgoingModifier == (Func<float, GameObject, Health, float>)Boost) Health.OutgoingModifier = null;
            if (life != null) life.RespawnStarted -= ResetStacks;
            if (movement != null) movement.SetSpeedBonus(this, 1f);
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Stacks == 0 || Time.time - lastKill < HoldSeconds || Time.time < nextDrain) return;
            nextDrain = Time.time + DrainSeconds;
            Set(Stacks - 1, -1);
        }

        /// <summary>Adds stacks directly (test arenas, scripted rewards).</summary>
        public void Add(int stacks)
        {
            lastKill = Time.time;
            nextDrain = Time.time + HoldSeconds;
            Set(Mathf.Min(MaxStacks, Stacks + stacks), stacks);
        }

        public void ResetStacks() => Set(0, -Stacks);

        private void ResetStacks(string _) => ResetStacks();

        private void OnKilled(Health dead, GameObject killer)
        {
            if (killer == null || !killer.transform.IsChildOf(transform) || !MeleeTargets.IsEnemy(dead)) return;
            int gain = context == KillKind.Finisher ? 3 : context == KillKind.Melee ? 2 : 1;
            Add(gain);
        }

        // Every hit the player deals, on anything but themselves.
        private float Boost(float amount, GameObject source, Health target)
        {
            if (Stacks == 0 || source == null || !source.transform.IsChildOf(transform) || target.transform.IsChildOf(transform)) return amount;
            return amount * DamageMultiplier;
        }

        private void Set(int stacks, int change)
        {
            stacks = Mathf.Clamp(stacks, 0, MaxStacks);
            if (stacks == Stacks && change <= 0) return;
            bool reachedMax = stacks == MaxStacks && Stacks < MaxStacks;
            Stacks = stacks;
            if (change > 0) LastGain = Time.time;
            if (movement != null) movement.SetSpeedBonus(this, SpeedMultiplier);
            if (reachedMax)
            {
                CameraShake.Add(0.25f);
                Sfx("laser_charge_full", 0.8f, 0.7f);
            }
            Changed?.Invoke(Stacks, change);
        }

        private void Sfx(string clip, float volume, float pitch) => SignalLost.Audio.Sfx.PlayAt(clip, transform.position, volume, pitch, 30f);
    }
}
