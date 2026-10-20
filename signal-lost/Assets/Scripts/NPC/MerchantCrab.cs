using System;
using System.Collections;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.NPC
{
    [RequireComponent(typeof(HermitCrab), typeof(Health), typeof(NavMeshAgent))]
    public sealed class MerchantCrab : MonoBehaviour
    {
        [SerializeField] private Transform returnPoint;
        [SerializeField] private TutorialPickup flaskDrop;
        [SerializeField] private GameObject carriedFlask;
        [SerializeField] private NpcDialogue dialogue;
        [SerializeField] private MerchantShop shop;
        [Tooltip("After surrendering, the crab leads the player home; it waits while the player is this far behind.")]
        [SerializeField, Min(2f)] private float leadWaitDistance = 12f;
        private HermitCrab brain;
        private HermitCrabRig rig;
        private Health health;
        private NavMeshAgent agent;
        private bool questStarted;
        private bool returning, encounterGreetingShown, friendlyGreetingShown;
        private TutorialPlayer player;
        private TutorialQuestFlow quest;
        private float nextRoute, nextLeadCall;
        private bool waitingForPlayer;
        private int nextHealthDialogue;
        private Coroutine damageDialogueRoutine;
        private readonly Queue<string[]> pendingDamageDialogue = new Queue<string[]>();
        // Fractions of Max Health, so changing the prefab's HP does not break dialogue pacing.
        private static readonly float[] HealthDialogueThresholds = { 0.75f, 0.5f, 0.25f };
        private static readonly string[][] HealthDialogueLines =
        {
            new[] { "Finders keepers, I'm not giving up easily!", "Claws up pal!" },
            new[] { "All right, you've made your point!", "This bucket's seen better days..." },
            new[] { "Easy! I'm a trader, not a punching bag!", "Let's discuss this like civilised crustaceans." }
        };
        public bool IsDefeated { get; private set; }
        public bool IsReturning => returning;
        /// <summary>True once the friendly crab has walked back and is standing beside Cavendish.</summary>
        public bool IsHome { get; private set; }
        /// <summary>True while it stops on the way home to let the player catch up.</summary>
        public bool IsWaitingForPlayer => returning && waitingForPlayer;
        public NpcDialogue Dialogue => dialogue;
        public MerchantShop Shop => shop;
        public event Action ArrivedHome;

        private void Awake()
        {
            player = FindFirstObjectByType<TutorialPlayer>();
            quest = FindFirstObjectByType<TutorialQuestFlow>();
            brain = GetComponent<HermitCrab>();
            rig = GetComponent<HermitCrabRig>();
            health = GetComponent<Health>();
            agent = GetComponent<NavMeshAgent>();
            brain.enabled = false;
            health.Invulnerable = true;
        }
        private void OnEnable() { health.Died += Defeated; health.Damaged += OnDamaged; }
        private void OnDisable()
        {
            health.Died -= Defeated; health.Damaged -= OnDamaged;
            StopDamageDialogue(); shop?.Close();
        }

        private void OnDamaged(Health value, float amount, GameObject source)
        {
            if (!questStarted || IsDefeated || value.IsDead) return;
            while (nextHealthDialogue < HealthDialogueThresholds.Length &&
                value.Normalized <= HealthDialogueThresholds[nextHealthDialogue])
                pendingDamageDialogue.Enqueue(HealthDialogueLines[nextHealthDialogue++]);
            if (pendingDamageDialogue.Count > 0 && damageDialogueRoutine == null)
                damageDialogueRoutine = StartCoroutine(PlayDamageDialogue());
        }

        private IEnumerator PlayDamageDialogue()
        {
            while (pendingDamageDialogue.Count > 0 && !IsDefeated)
            {
                dialogue.Speak(pendingDamageDialogue.Dequeue());
                yield return new WaitUntil(() => !dialogue.IsSpeaking || IsDefeated);
            }
            damageDialogueRoutine = null;
        }

        private void StopDamageDialogue()
        {
            if (damageDialogueRoutine != null) StopCoroutine(damageDialogueRoutine);
            damageDialogueRoutine = null;
            pendingDamageDialogue.Clear();
        }

        public void BeginQuest()
        {
            if (questStarted || IsDefeated) return;
            questStarted = true;
            health.Invulnerable = false;
            brain.enabled = true;
        }

        public void Interact()
        {
            if (!IsDefeated) return;
            if (quest != null && quest.TalkToMerchant()) return;
            OpenShop();
        }

        /// <summary>The friendly crab's own small talk and shop, once the tutorial no longer needs it.</summary>
        public void OpenShop(bool greet = true)
        {
            if (!greet) friendlyGreetingShown = true;
            if (!friendlyGreetingShown)
            {
                friendlyGreetingShown = true;
                dialogue.SpeakManual("Well, if it isn't my toughest customer. Welcome to Bucket & Barter!");
                return;
            }
            if (dialogue.IsManual && dialogue.AdvanceManual() && dialogue.IsManual) return;
            shop.Toggle();
        }

        private void Defeated(Health _)
        {
            if (IsDefeated) return;
            IsDefeated = true;
            StopDamageDialogue();
            health.Invulnerable = true;
            brain.enabled = false;
            carriedFlask.SetActive(false);
            Vector3 drop = transform.position + transform.right * 2f;
            // Put the reward on the actual ground, never inside the defeated crab.
            if (Physics.Raycast(drop + Vector3.up * 8, Vector3.down, out RaycastHit hit, 20, ~0, QueryTriggerInteraction.Ignore))
                drop = hit.point + Vector3.up * 0.03f;
            flaskDrop.transform.position = drop;
            flaskDrop.gameObject.SetActive(true);
            dialogue.Speak("All right! Claws down!",
                "Trading is my business. Brawling is clearly yours.",
                "Let's call that an aggressive negotiation.");
            StartCoroutine(Recover());
        }

        private IEnumerator Recover()
        {
            yield return new WaitForSeconds(1.2f);
            health.Restore();
            rig.ResetPose();
            foreach (Collider item in GetComponents<Collider>()) item.enabled = true;
            agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3, NavMesh.AllAreas)) agent.Warp(hit.position);
            agent.updateRotation = true;
            agent.speed = 2.5f;
            agent.stoppingDistance = 0.6f;
            agent.isStopped = false;
            returning = true;
        }

        private void Update()
        {
            if (!IsDefeated && !encounterGreetingShown && player != null && !player.IsRespawning &&
                Vector3.Distance(player.transform.position, transform.position) <= 9f)
            {
                encounterGreetingShown = true;
                if (nextHealthDialogue == 0) dialogue.Speak("Oi! Nice wreck you've got there, mechanic.", "Before you ask: the bucket isn't for sale!");
            }
            if (!returning || !agent.isOnNavMesh || returnPoint == null) return;
            rig.MoveSpeed = agent.velocity.magnitude;
            UpdateLead();
            if (waitingForPlayer) return;
            if (Time.time >= nextRoute)
            {
                nextRoute = Time.time + 1;
                agent.SetDestination(returnPoint.position);
            }
            if (!agent.pathPending && agent.hasPath && agent.remainingDistance <= agent.stoppingDistance + 0.15f)
            {
                returning = false;
                agent.isStopped = true;
                rig.MoveSpeed = 0;
                transform.rotation = returnPoint.rotation;
                IsHome = true;
                ArrivedHome?.Invoke();
            }
        }

        // Leading the player home: stop and wave them on while they lag behind, but never wait for a player who
        // is already closer to home than the crab (for example after respawning at the crash site).
        private void UpdateLead()
        {
            if (player == null) { waitingForPlayer = false; return; }
            Vector3 home = returnPoint.position;
            float gap = Vector3.Distance(player.transform.position, transform.position);
            bool behind = Vector3.Distance(player.transform.position, home) > Vector3.Distance(transform.position, home) + 2f;
            bool wait = gap > leadWaitDistance && behind && !player.IsRespawning;
            if (wait != waitingForPlayer)
            {
                waitingForPlayer = wait;
                agent.isStopped = wait;
                if (wait) nextLeadCall = Time.time + 1.5f;
            }
            if (!wait) return;
            rig.MoveSpeed = 0f;
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toPlayer), 180f * Time.deltaTime);
            if (Time.time >= nextLeadCall)
            {
                nextLeadCall = Time.time + 9f;
                dialogue.Speak(3.5f, player.HasFlask ? "This way, mechanic! Shop's back at your wreck." : "Grab your flask, then follow me! I'll wait.");
            }
        }

        public void ResetAfterPlayerRespawn()
        {
            shop.Close();
            if (IsDefeated) return; // Friendly merchant and dropped/collected flask persist this session.
            StopDamageDialogue();
            nextHealthDialogue = 0;
            dialogue.Speak();
            brain.enabled = true;
            brain.ResetEncounter();
            brain.enabled = questStarted;
            health.Invulnerable = !questStarted;
        }
    }
}
