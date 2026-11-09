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
    /// One boss's fight in the level: the boss is not there until the story arms it (Cavendish gave directions), then
    /// coming within <see cref="triggerRadius"/> of its arena plays its entrance and starts the fight (its fodder
    /// spawner joins in through the boss). Beaten, it reports back to the story.
    /// </summary>
    public sealed class MudBossEncounter : MonoBehaviour
    {
        public enum State { Waiting, Armed, Cutscene, Fighting, Defeated }

        [SerializeField] private MudBoss boss;
        [SerializeField, Min(5f)] private float triggerRadius = 30f;

        public State Current { get; private set; }
        public MudBoss Boss => boss;
        public event Action Started, Defeated;

        private PlayerLife player;

        public void Configure(MudBoss target, float radius)
        {
            boss = target;
            triggerRadius = radius;
        }

        private void Awake()
        {
            player = FindAnyObjectByType<PlayerLife>();
            if (boss != null) boss.gameObject.SetActive(false);
            Signal(false);
        }

        private void OnEnable()
        {
            if (boss != null) boss.Beaten += OnBeaten;
        }

        private void OnDisable()
        {
            if (boss != null) boss.Beaten -= OnBeaten;
        }

        /// <summary>The story has given the directions: the boss waits for the player.</summary>
        public void Arm()
        {
            if (Current != State.Waiting) return;
            Current = State.Armed;
            Signal(true);
        }

        private void Update()
        {
            if (Current != State.Armed || player == null || player.IsRespawning || UiMenus.AnyOpen) return;
            Vector3 offset = player.transform.position - transform.position;
            offset.y = 0f;
            if (offset.magnitude <= triggerRadius) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            Current = State.Cutscene;
            Signal(false);
            var cine = new BossEntrance(this);
            cine.Begin();
            boss.enabled = false;
            var agent = boss.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            boss.gameObject.SetActive(true);
            if (!boss.ShowsOwnTitle) MudHud.Title(boss.Title, boss.Subtitle, 3.2f);
            boss.FaceCamera = cine.Camera;
            yield return boss.Entrance(cine);
            boss.FaceCamera = null;
            // The entrance leaves the boss where its fight begins; only a skipped or wandering one is put back.
            Vector3 start = boss.FightStart(transform.position);
            if (cine.Skipped || Vector3.Distance(boss.transform.position, start) > 4f) boss.transform.position = start;
            if (boss.WalksAfterEntrance) AgentUtil.Reenable(agent, 8f);
            boss.enabled = true;
            cine.End();
            Current = State.Fighting;
            Started?.Invoke();
        }

        private void OnBeaten(MudBoss _)
        {
            Current = State.Defeated;
            Defeated?.Invoke();
        }

        private void Signal(bool on)
        {
            if (TryGetComponent(out SignalLost.Scanner.QuestSignalSource signal)) signal.enabled = on;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, triggerRadius);
        }
    }
}
