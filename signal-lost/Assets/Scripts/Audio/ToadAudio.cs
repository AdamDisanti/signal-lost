using SignalLost.Combat;
using SignalLost.Enemies.Meadow;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Audio
{
    /// <summary>
    /// Sounds for the Chief Toad, added automatically by <see cref="SoundDirector"/>. Built from the placeholder set,
    /// pitched down for something this big:
    /// - heavy thuds as he hops, a deep clang when hit, a croak and a burst of coolant when he dies;
    /// - the water beam: a rising charge, a blast as it fires, then a rushing jet (the water loop, sped up) while it
    ///   runs, the same for the tidal spin;
    /// - the blades: a whoosh per volley and a splash where each one bursts;
    /// - the slam: a grunt at take-off, a crash on landing and a rush of water as the wave spreads;
    /// - the scream: a swelling charge, a long low croak and a blast; if it stuns you, your ears ring until it wears off;
    /// - the coolant cracking (phase 2) and overflowing (phase 3): a fizzle and a sizzle while it sprays.
    /// It listens to <see cref="ChiefToad.Cued"/>, so the boss itself has no sound code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToadAudio : MonoBehaviour
    {
        private const float Reach = 45f;

        private ChiefToad toad;
        private Health health;
        private NavMeshAgent agent;
        private FirstPersonController player;
        private SfxLoop jet, wave, sizzle;
        private AudioSource ringing;
        private float nextHit, nextThud, waveUntil;

        private void Awake()
        {
            toad = GetComponent<ChiefToad>();
            health = GetComponent<Health>();
            agent = GetComponent<NavMeshAgent>();
            jet = Loop("water_lap", 0.9f, 1.8f, "Water Beam Sound");
            wave = Loop("water_lap", 0.8f, 2.3f, "Splash Wave Sound");
            sizzle = Loop("electric_sizzle", 0.45f, 0.8f, "Coolant Sizzle Sound");
        }

        private SfxLoop Loop(string clip, float volume, float pitch, string childName)
        {
            SfxLoop loop = Sfx.AddLoop(gameObject, clip, volume, true, 4f, Reach, childName: childName);
            if (loop == null) return null;
            loop.Source.pitch = pitch;
            loop.Level = 0f;
            return loop;
        }

        private void OnEnable()
        {
            if (toad != null) toad.Cued += OnCue;
            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Died += OnDied;
            }
            WaterBlade.Splashed += OnBladeSplash;
        }

        private void OnDisable()
        {
            if (toad != null) toad.Cued -= OnCue;
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }
            WaterBlade.Splashed -= OnBladeSplash;
            StopRinging();
        }

        private void Update()
        {
            if (toad == null) return;
            bool alive = !toad.IsDead;
            string state = toad.StateName;
            Fade(jet, alive && (state == "Beam" || state == "Spin") ? 1f : 0f, 8f);
            Fade(wave, alive && Time.time < waveUntil ? 1f : 0f, 2.5f);
            Fade(sizzle, alive && (state == "Overflow" || toad.InPhaseShift) ? 1f : 0f, 3f);

            // A heavy thud with each hop while he moves.
            float speed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
            if (alive && speed > 0.6f && Time.time >= nextThud)
            {
                nextThud = Time.time + Mathf.Lerp(0.6f, 0.38f, Mathf.Clamp01(speed / 5f));
                Sfx.PlayAt(Sfx.Pick("footstep_grass", 4), transform.position, 0.6f, Random.Range(0.42f, 0.5f), 30f);
            }

            // The ringing in your ears stops when the stun does.
            if (ringing != null && (player == null || !player.IsStunned)) StopRinging();
        }

        private static void Fade(SfxLoop loop, float target, float rate)
        {
            if (loop != null) loop.Level = Mathf.MoveTowards(loop.Level, target, Time.deltaTime * rate);
        }

        private Vector3 Mouth => transform.position + Vector3.up * 2f + transform.forward * 1.8f;

        private void OnCue(ChiefToad.Cue cue)
        {
            switch (cue)
            {
                case ChiefToad.Cue.BeamCharge:
                    Sfx.PlayAt("laser_charge", Mouth, 0.9f, 0.6f, Reach);
                    break;
                case ChiefToad.Cue.BeamFire:
                    Sfx.PlayAt("laser_blast", Mouth, 0.8f, 0.75f, Reach);
                    break;
                case ChiefToad.Cue.SpinCharge:
                    Sfx.PlayAt("laser_charge", Mouth, 1f, 0.5f, Reach);
                    break;
                case ChiefToad.Cue.Blades:
                    Sfx.PlayAt(Sfx.Pick("laser_swing", 2), Mouth, 0.9f, Random.Range(0.7f, 0.8f), Reach);
                    break;
                case ChiefToad.Cue.Jump:
                    Sfx.PlayAt("player_jump", transform.position, 1f, 0.45f, Reach);
                    break;
                case ChiefToad.Cue.Land:
                    Sfx.PlayAt("laser_slam", transform.position, 1f, 0.6f, Reach + 10f);
                    Sfx.PlayAt("player_land", transform.position, 1f, 0.4f, Reach);
                    waveUntil = Time.time + 1.1f;
                    if (wave != null) wave.Level = 1f;
                    break;
                case ChiefToad.Cue.ScreamCharge:
                    Sfx.PlayAt("laser_charge_hold", Mouth, 0.8f, 0.45f, Reach);
                    break;
                case ChiefToad.Cue.Scream:
                    Sfx.PlayAt("crab_whimper", Mouth, 1f, 0.42f, Reach + 10f);
                    Sfx.PlayAt("laser_blast", Mouth, 0.6f, 0.5f, Reach);
                    if (player == null) player = FindAnyObjectByType<FirstPersonController>();
                    if (player != null && player.IsStunned && ringing == null) ringing = Sfx.Play("wake_ringing", 0.45f);
                    break;
                case ChiefToad.Cue.CoolantCrack:
                case ChiefToad.Cue.Overflow:
                    Sfx.PlayAt("laser_fizzle", transform.position + Vector3.up * 2.2f, 1f, 0.6f, Reach);
                    break;
            }
        }

        private void OnDamaged(Health _, float amount, GameObject source)
        {
            if (Time.time < nextHit) return;
            nextHit = Time.time + 0.1f;
            Sfx.PlayAt(Sfx.Pick("crab_hit", 2), transform.position + Vector3.up * 1.4f, 0.8f, Random.Range(0.5f, 0.58f), Reach);
        }

        private void OnDied(Health _)
        {
            Sfx.PlayAt("crab_death", transform.position + Vector3.up * 1.4f, 1f, 0.45f, Reach + 10f);
            Sfx.PlayAt("laser_blast", transform.position + Vector3.up * 2f, 0.7f, 0.55f, Reach);
            StopRinging();
        }

        private void OnBladeSplash(Vector3 at, bool hitPlayer) =>
            Sfx.PlayAt("bolt_impact", at, hitPlayer ? 0.8f : 0.5f, Random.Range(0.55f, 0.7f), 30f);

        private void StopRinging()
        {
            // Only stop the source if it's still ours (the pool may have reused it for another sound).
            if (ringing != null && ringing.clip != null && ringing.clip.name == "wake_ringing") ringing.Stop();
            ringing = null;
        }
    }
}
