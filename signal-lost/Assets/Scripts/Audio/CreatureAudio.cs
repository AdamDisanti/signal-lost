using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.NPC;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Audio
{
    /// <summary>
    /// Sounds for a hermit crab (the basic crab, the Bucket Brothers and Eugine), added automatically by
    /// <see cref="SoundDirector"/>: leg skittering while it moves, a hollow bucket clang when hit, a hard clang when it
    /// blocks, a squeak when it goes down, and Eugine's whimper when it surrenders. Bigger crabs sound lower.
    /// The claw snip when a crab hits the player is played by <see cref="PlayerAudio"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreatureAudio : MonoBehaviour
    {
        private HermitCrab crab;
        private Health health;
        private NavMeshAgent agent;
        private MerchantCrab merchant;
        private SfxLoop skitter;
        private float pitch = 1f, nextHit;
        private bool wasDefeated;

        private void Awake()
        {
            crab = GetComponent<HermitCrab>();
            health = GetComponent<Health>();
            agent = GetComponent<NavMeshAgent>();
            merchant = GetComponent<MerchantCrab>();
            float size = Mathf.Max(0.3f, transform.lossyScale.x);
            pitch = Mathf.Clamp(1f / Mathf.Pow(size, 0.45f), 0.6f, 1.5f);
            skitter = Sfx.AddLoop(gameObject, "crab_skitter", 0.5f, true, 1.5f, 18f, childName: "Skitter Sound");
            if (skitter != null)
            {
                skitter.Source.pitch = pitch;
                skitter.Level = 0f;
            }
            wasDefeated = merchant != null && merchant.IsDefeated;
        }

        private void OnEnable()
        {
            if (health == null) return;
            health.Damaged += OnDamaged;
            health.Blocked += OnBlocked;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (health == null) return;
            health.Damaged -= OnDamaged;
            health.Blocked -= OnBlocked;
            health.Died -= OnDied;
        }

        private void Update()
        {
            if (skitter != null)
            {
                bool dead = crab != null && crab.IsDead;
                float speed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
                skitter.Level = Mathf.MoveTowards(skitter.Level, dead ? 0f : Mathf.Clamp01(speed / 2.5f), Time.deltaTime * 4f);
            }

            if (merchant != null)
            {
                if (merchant.IsDefeated && !wasDefeated) Sfx.PlayAt("crab_whimper", Center, 0.8f, pitch, 35f);
                wasDefeated = merchant.IsDefeated;
            }
        }

        private Vector3 Center => transform.position + Vector3.up * 0.6f * transform.lossyScale.y;

        private void OnDamaged(Health _, float amount, GameObject source)
        {
            if (Time.time < nextHit) return;
            nextHit = Time.time + 0.08f;
            Sfx.PlayAt(Sfx.Pick("crab_hit", 2), Center, 0.7f, pitch * Random.Range(0.94f, 1.06f), 32f);
        }

        private void OnBlocked(Health _, float amount, GameObject source) =>
            Sfx.PlayAt("crab_block", Center, 0.75f, Random.Range(0.95f, 1.05f), 32f);

        private void OnDied(Health _) => Sfx.PlayAt("crab_death", Center, 0.75f, pitch * Random.Range(0.95f, 1.05f), 35f);
    }
}
