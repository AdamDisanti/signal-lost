using System;
using System.Collections;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.Inventory;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.NPC
{
    [RequireComponent(typeof(HermitCrab), typeof(Health), typeof(NavMeshAgent))]
    public sealed class MerchantCrab : MonoBehaviour
    {
        [SerializeField] private Transform returnPoint;
        [Tooltip("Where it settles once home (on a camp log). Empty: it stays at the return point.")]
        [SerializeField] private Transform seat;
        [Tooltip("Walking speed while leading the player home.")]
        [SerializeField, Min(0.5f)] private float leadSpeed = 3.75f;
        [SerializeField] private ItemPickup flaskDrop;
        [SerializeField] private GameObject carriedFlask;
        [SerializeField] private NpcDialogue dialogue;
        [SerializeField] private MerchantShop shop;
        [Tooltip("Free-play scenes: the crab is already friendly (no fight, no flask drop) and just runs its shop.")]
        [SerializeField] private bool startFriendly;
        [Tooltip("After surrendering, the crab leads the player home; it waits while the player is this far behind.")]
        [SerializeField, Min(2f)] private float leadWaitDistance = 12f;

        [Header("Cousins (the Bucket Brothers)")]
        [Tooltip("The same ten cousins fight beside the crab in its quest and answer when the angry merchant calls. " +
                 "They wait, harmless, until the quest begins; in free play they start despawned.")]
        [SerializeField] private CrabCousin[] cousins = Array.Empty<CrabCousin>();
        [Tooltip("Hits on the friendly crab before it calls its cousins.")]
        [SerializeField, Min(1)] private int hitsBeforeAmbush = 6;
        [Tooltip("Each hit is forgotten after this many seconds without another.")]
        [SerializeField, Min(1f)] private float angerCooldown = 10f;
        private HermitCrab brain;
        private HermitCrabRig rig;
        private Health health;
        private NavMeshAgent agent;
        private bool questStarted;
        private bool returning, encounterGreetingShown, friendlyGreetingShown;
        private PlayerLife player;
        private PlayerGear playerGear;
        private float nextRoute, nextLeadCall;
        private bool waitingForPlayer;
        private int nextHealthDialogue;
        private Coroutine damageDialogueRoutine;
        private readonly Queue<string[]> pendingDamageDialogue = new Queue<string[]>();
        private readonly List<HermitCrab> ambush = new List<HermitCrab>();
        private int anger;
        private float nextCalm;
        private static readonly string[] Warnings =
        {
            "Hey! That's not nice!",
            "Hey, don't hit me! I'm a small business!",
            "Ow! Do that again and I'm calling my cousins.",
            "I'm SERIOUS, mechanic. They're very big cousins.",
            "Last warning! One more and it's bucket time!"
        };
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
        /// <summary>Recent hits on the friendly crab; one is forgiven every few seconds without another.</summary>
        public int Anger => anger;
        public bool AmbushActive => ambush.Count > 0;
        public IReadOnlyList<HermitCrab> Ambush => ambush;
        public IReadOnlyList<CrabCousin> Cousins => cousins;
        /// <summary>True when no cousin is still fighting (all defeated or despawned).</summary>
        public bool CousinsDown
        {
            get
            {
                foreach (CrabCousin cousin in cousins) if (cousin != null && !cousin.IsDown) return false;
                return true;
            }
        }

        private void Awake()
        {
            player = FindAnyObjectByType<PlayerLife>();
            playerGear = player != null ? player.GetComponent<PlayerGear>() : null;
            brain = GetComponent<HermitCrab>();
            rig = GetComponent<HermitCrabRig>();
            health = GetComponent<Health>();
            agent = GetComponent<NavMeshAgent>();
            brain.enabled = false;
            brain.Stunnable = false; // a mini-boss: the combo finisher does not stun it
            health.Invulnerable = true;
            foreach (CrabCousin cousin in cousins) if (cousin != null) cousin.Crab.enabled = false; // until the quest
        }
        private void OnEnable() { health.Died += Defeated; health.Damaged += OnDamaged; health.Blocked += OnBlocked; }
        private void OnDisable()
        {
            health.Died -= Defeated; health.Damaged -= OnDamaged; health.Blocked -= OnBlocked;
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
            foreach (CrabCousin cousin in cousins) if (cousin != null && !cousin.IsDown) cousin.Crab.enabled = true;
        }

        private void Start()
        {
            if (!startFriendly) return;
            IsDefeated = IsHome = friendlyGreetingShown = true;
            brain.enabled = false;
            health.Invulnerable = true;
            if (carriedFlask != null) carriedFlask.SetActive(false);
            foreach (CrabCousin cousin in cousins) if (cousin != null) cousin.Despawn();
        }

        /// <summary>The story has already introduced the crab, so the first visit opens the shop straight away.</summary>
        public void MarkGreeted() => friendlyGreetingShown = true;

        /// <summary>The friendly crab's own small talk and shop, once the story no longer needs it.</summary>
        public void OpenShop(bool greet = true)
        {
            if (!IsDefeated) return;
            if (AmbushActive)
            {
                dialogue.Warn("Busy! Watching my cousins teach you some manners.");
                return;
            }
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
            // It floats and turns above the grass so it is easy to spot.
            Transform bottle = flaskDrop.transform.Find("Visual");
            if (bottle != null && flaskDrop.GetComponent<SignalLost.Progression.PickupSpin>() == null)
            {
                bottle.localPosition += Vector3.up * 0.45f;
                flaskDrop.gameObject.AddComponent<SignalLost.Progression.PickupSpin>().Model = bottle;
            }
            if (CousinsDown)
                dialogue.Speak("All right! Claws down!",
                    "Trading is my business. Brawling is clearly yours.",
                    "Let's call that an aggressive negotiation.");
            else
                dialogue.Speak("All right! Claws down!", "But I can't call the cousins off once they get going!");
            StartCoroutine(Recover());
        }

        private IEnumerator Recover()
        {
            yield return new WaitForSeconds(1.2f);
            health.Restore();
            rig.ResetPose();
            foreach (Collider item in GetComponents<Collider>()) item.enabled = true;
            if (!CousinsDown)
            {
                // It has given up, but the cousins have not: it waits for the brawl to end before leading the way.
                yield return new WaitUntil(() => CousinsDown);
                dialogue.Speak(4f, "...And that's the whole family. Fine, FINE. I'll come quietly.");
            }
            agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3, NavMesh.AllAreas)) agent.Warp(hit.position);
            agent.updateRotation = true;
            agent.speed = leadSpeed;
            agent.stoppingDistance = 0.6f;
            agent.isStopped = false;
            returning = true;
        }

        private void Update()
        {
            if (IsDefeated) UpdateAnger();
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
                if (seat != null) StartCoroutine(TakeSeat());
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
                dialogue.Speak(3.5f, playerGear != null && playerGear.Has(PlayerItem.Flask) ? "This way, mechanic! Your camp's just ahead." : "Grab your flask, then follow me! I'll wait.");
            }
        }

        // ---------- Angry merchant (meeting notes 2026-10-20) ----------

        // The friendly crab cannot be hurt, but it notices every hit: warnings first, then the Bucket Brothers.
        private void OnBlocked(Health _, float amount, GameObject source)
        {
            if (!IsDefeated || source == null || source.GetComponentInParent<PlayerLife>() == null) return;
            rig.Flinch(false);
            if (AmbushActive || !CousinsDown) return; // still busy with the quest brawl
            anger++;
            nextCalm = Time.time + angerCooldown;
            if (anger >= hitsBeforeAmbush) StartAmbush();
            else dialogue.Warn(Warnings[Mathf.Min(anger, Warnings.Length) - 1]);
        }

        private void StartAmbush()
        {
            shop.Close();
            dialogue.Warn("THAT'S IT! Bucket Brothers, ASSEMBLE!");
            // Always the same ten cousins, back at full health in a ring around the merchant.
            for (int i = 0; i < cousins.Length; i++)
            {
                if (cousins[i] == null) continue;
                float angle = (i + UnityEngine.Random.value * 0.5f) / cousins.Length * Mathf.PI * 2f;
                Vector3 spot = transform.position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * UnityEngine.Random.Range(4.5f, 8f);
                if (!NavMesh.SamplePosition(spot, out NavMeshHit hit, 4f, NavMesh.AllAreas)) continue;
                Vector3 face = player != null ? player.transform.position - hit.position : -transform.forward;
                face.y = 0f;
                cousins[i].Summon(hit.position, face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face) : Quaternion.identity);
                ambush.Add(cousins[i].Crab);
            }
            if (ambush.Count == 0) { EndAmbush(); return; }
            StartCoroutine(ProvokeAmbush());
        }

        // Spawned crabs finish Start (and find the NavMesh) on their first frame; then they charge.
        private IEnumerator ProvokeAmbush()
        {
            yield return null;
            yield return null;
            foreach (HermitCrab crab in ambush) if (crab != null) crab.Provoke();
        }

        private void UpdateAnger()
        {
            if (AmbushActive)
            {
                foreach (HermitCrab crab in ambush) if (crab != null && !crab.IsDead) return;
                EndAmbush();
                return;
            }
            if (anger > 0 && Time.time >= nextCalm)
            {
                anger--;
                nextCalm = Time.time + angerCooldown;
            }
        }

        // Ends the ambush the way the fight ended: the crab surrenders again and the shop stays open.
        private void EndAmbush()
        {
            // Fallen cousins despawn by themselves; the rest go home too.
            foreach (HermitCrab crab in ambush)
                if (crab != null && !crab.IsDead && crab.TryGetComponent(out CrabCousin cousin)) cousin.Despawn();
            ambush.Clear();
            anger = 0;
            dialogue.Speak(4f, "All right, all right! Claws down... AGAIN.", "No hard feelings? The shop's still open.");
        }

        // A little hop up onto its log by the fire.
        private IEnumerator TakeSeat()
        {
            agent.enabled = false;
            Vector3 from = transform.position;
            Quaternion turnFrom = transform.rotation;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                float k = t / 0.5f;
                transform.SetPositionAndRotation(Vector3.Lerp(from, seat.position, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.6f,
                    Quaternion.Slerp(turnFrom, seat.rotation, k));
                yield return null;
            }
            transform.SetPositionAndRotation(seat.position, seat.rotation);
        }

        public void ResetAfterPlayerRespawn()
        {
            shop.Close();
            if (AmbushActive) EndAmbush();
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
