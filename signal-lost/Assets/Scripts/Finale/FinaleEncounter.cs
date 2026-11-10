using System.Collections;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies;
using SignalLost.Enemies.Desert;
using SignalLost.Player;
using SignalLost.Progression;
using SignalLost.UI;
using UnityEngine;

namespace SignalLost.Finale
{
    /// <summary>
    /// Runs the final fight (docs/final-boss.md): the intro when the player walks into the arena, the shield tasks
    /// between phases, the adds, the phase checkpoint when the player dies, and the end.
    /// <list type="bullet">
    /// <item><b>The horde (11-09):</b> Cavendish's robot creatures pour in for the whole fight, faster and more of them
    /// the longer it goes, the later the phase, and while the shield is up.</item>
    /// <item><b>Robot mini-bosses (11-09):</b> when the shield goes up, three of his robot copies of earlier bosses come
    /// in (<see cref="FinaleRobots"/>): Robo-Toad, Mecha-Buzzard and Burn-Bot, then Mecha-Worm, Witch-Bot and Silt-Bot.
    /// They keep fighting after the shield breaks.</item>
    /// <item><b>Phase 1 task (built):</b> the shield draws power from three conduits hidden in the scrap around the arena.
    /// They show on the scanner; break them all and the shield fails.</item>
    /// <item><b>Phase 2 task (PLACEHOLDER):</b> the planned "carry coolant cells to the vents" isn't built, so it reuses
    /// the conduits (a second set of four) to keep the fight playable end to end.</item>
    /// <item><b>Phase 3:</b> the planned "ground the mech on the launch clamps" and the Reactor Core finale aren't built;
    /// phase 3 just fights to zero.</item>
    /// <item><b>The end (PLACEHOLDER):</b> the meltdown, the escape run and the launch aren't built; a message says so.</item>
    /// </list>
    /// </summary>
    public sealed class FinaleEncounter : MonoBehaviour
    {
        public struct Add
        {
            public GameObject Go;
            public Health Health;
        }

        public enum State { Waiting, Intro, Fighting, Won }

        /// <summary>The adds in the arena right now (Eugene's crew goes after these first).</summary>
        public static readonly List<Add> Adds = new List<Add>();

        [SerializeField] private CavendishMech mech;
        [Tooltip("The phase 1 task: conduits that feed the shield.")]
        [SerializeField] private ShieldConduit[] conduits = new ShieldConduit[0];
        [Tooltip("PLACEHOLDER for the phase 2 task (coolant cells): a second set of conduits.")]
        [SerializeField] private ShieldConduit[] phaseTwoConduits = new ShieldConduit[0];
        [SerializeField] private CombatZone arena;
        [Tooltip("The fight starts when the player is this close to the arena's middle.")]
        [SerializeField, Min(5f)] private float triggerRadius = 34f;
        [Tooltip("The horde: creatures Cavendish rebuilt as robots, sent in all fight (any enemy prefab; swap in each island's own as they're built).")]
        [SerializeField] private GameObject[] addPrefabs = new GameObject[0];
        [Tooltip("Seconds between horde spawns at the start, and the shortest it gets.")]
        [SerializeField, Min(0.5f)] private float hordeInterval = 5.5f, hordeFastest = 1f;
        [Tooltip("Most horde creatures alive at once at the start, and the most ever.")]
        [SerializeField, Min(1)] private int hordeCap = 6, hordeMaxCap = 26;
        [Tooltip("The bosses the robot mini-bosses are copied from.")]
        [SerializeField] private FinaleRobots.Sources robotSources = new FinaleRobots.Sources();
        [Tooltip("Robot mini-bosses' health, as a share of the original's (three come at once).")]
        [SerializeField, Range(0.2f, 1.5f)] private float robotHealth = 0.7f;
        [Tooltip("Eugene first, then his cousins: who answers the call-in.")]
        [SerializeField] private GameObject[] crewPrefabs = new GameObject[0];

        private static readonly string[] IntroLines =
        {
            "Cavendish: Ah, mechanic. Right on time. You always were dependable.",
            "Eugene: Cavendish? Buddy? What... what IS that thing?",
            "Cavendish: Every part you fetched. Kept SAFE, just as I promised. Now they keep ME safe.",
            "Cavendish: Did you really think I'd let you fly off and leave me on this rock? Again?",
            "Eugene: I can't believe this. We shared a log, Cav!"
        };
        private static readonly string[] ShieldLines =
        {
            "Cavendish: Shields up! You'll never find the power couplings. I hid them myself.",
            "Cavendish: Not so fast! Backup power!"
        };
        private static readonly string[] PhaseLines =
        {
            "",
            "Cavendish: You broke my shield?! Fine. FULL THROTTLE!",
            "Cavendish: Enough! Every system, everything you gave me, all at once!"
        };
        private static readonly string[] RetryLines =
        {
            "Cavendish: Up again? How very dependable of you.",
            "Cavendish: Shall we try that again, mechanic?",
            "Eugene: Don't give up! He's just a big rat in a can!"
        };

        public State Current { get; private set; } = State.Waiting;
        public CavendishMech Mech => mech;
        public GameObject[] CrewPrefabs => crewPrefabs;
        public CrewCallIn Crew { get; private set; }
        public int ConduitsLeft { get; private set; }
        public int ConduitsTotal { get; private set; }
        public int Retries { get; private set; }
        /// <summary>The robot mini-bosses sent in so far (alive or not).</summary>
        public IReadOnlyList<FinaleRobots.Robot> Robots => robots;
        /// <summary>Seconds of fighting so far (the horde grows with it).</summary>
        public float FightTime => Current == State.Fighting ? Time.time - fightStart : 0f;
        /// <summary>How many horde creatures may be alive right now (it grows with time, phase and the shield).</summary>
        public int HordeCap => Mathf.Min(hordeMaxCap, hordeCap + 3 * (mech.Phase - 1) + Mathf.FloorToInt(FightTime / 60f * 2f) + (mech.Shielded ? 4 : 0));

        private PlayerLife player;
        private ShieldConduit[] task = new ShieldConduit[0];
        private Coroutine adds;
        private int retryLine;
        private float fightStart;
        private readonly List<FinaleRobots.Robot> robots = new List<FinaleRobots.Robot>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Adds.Clear();

        private void Start()
        {
            player = FindAnyObjectByType<PlayerLife>();
            if (player != null)
            {
                Crew = CrewCallIn.Ensure(player.gameObject, this);
                player.Health.Died += OnPlayerDied;
                player.Respawned += OnPlayerRespawned;
            }
            mech.ShieldRaised += OnShieldRaised;
            mech.PhaseStarted += OnPhaseStarted;
            mech.Beaten += OnBeaten;
            foreach (ShieldConduit c in conduits) c.Broken += OnConduitBroken;
            foreach (ShieldConduit c in phaseTwoConduits) c.Broken += OnConduitBroken;
            FinaleHud.Ensure(this);
            if (arena != null) mech.SetArena(transform.position, arena.Radius - 4f);
            GameplayOverlay.Instance.SetQuest("Safe Keeping", "Cavendish is waiting at the wreck.");
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.Health.Died -= OnPlayerDied;
                player.Respawned -= OnPlayerRespawned;
            }
        }

        private void Update()
        {
            if (Current == State.Waiting && player != null && !player.IsRespawning)
            {
                Vector3 offset = player.transform.position - transform.position;
                offset.y = 0f;
                if (offset.magnitude <= triggerRadius) StartCoroutine(Intro());
            }
            Adds.RemoveAll(a => a.Go == null || a.Health == null || a.Health.IsDead);
            // The robot mini-bosses' own top-of-screen bars stay hidden; the finale HUD lists them.
            foreach (FinaleRobots.Robot robot in robots)
                if (robot.MiniBoss != null) MiniBossHud.Unregister(robot.MiniBoss);
        }

        // ---------- Intro ----------

        private IEnumerator Intro()
        {
            Current = State.Intro;
            GameplayOverlay.Instance.SetQuest("Safe Keeping", "...");
            foreach (string line in IntroLines)
            {
                Say(line, 3.4f);
                yield return new WaitForSeconds(3.4f);
            }
            mech.Wake();
            yield return new WaitForSeconds(2.4f);
            BeginFighting();
        }

        private void BeginFighting()
        {
            Current = State.Fighting;
            Crew.Enabled = true;
            fightStart = Time.time;
            if (adds != null) StopCoroutine(adds);
            adds = StartCoroutine(Horde());
        }

        /// <summary>Skips the intro's dialogue (for tests).</summary>
        public void SkipIntro()
        {
            if (Current != State.Waiting && Current != State.Intro) return;
            StopAllCoroutines();
            mech.Wake();
            BeginFighting();
        }

        private static void Say(string line, float seconds)
        {
            int colon = line.IndexOf(':');
            string speaker = colon > 0 ? line.Substring(0, colon) : "";
            DialogueUi.Instance?.Subtitle(speaker, colon > 0 ? line.Substring(colon + 1).Trim() : line, seconds);
        }

        // ---------- Phases and shield tasks ----------

        private void OnPhaseStarted(int phase)
        {
            if (phase - 1 < PhaseLines.Length && PhaseLines[phase - 1].Length > 0) Say(PhaseLines[phase - 1], 3.5f);
            GameplayOverlay.Instance.SetQuest("Safe Keeping", $"Phase {phase} of 3: wreck Cavendish's mech. The glowing Reactor Core on its chest takes extra damage.\n" +
                $"{SignalLost.Core.KeyBindings.Hint("call_crew")} Call Eugene's crew.");
        }

        private void OnShieldRaised(int phase)
        {
            Say(ShieldLines[Mathf.Clamp(phase - 1, 0, ShieldLines.Length - 1)], 3.5f);
            task = phase == 1 ? conduits : phaseTwoConduits;
            ConduitsTotal = 0;
            foreach (ShieldConduit c in task)
                if (c != null && !c.IsBroken) { c.SetLive(true); ConduitsTotal++; }
            ConduitsLeft = ConduitsTotal;
            UpdateTaskText();
            SendRobots(phase == 1 ? FinaleRobots.FirstShield : FinaleRobots.SecondShield);
            if (ConduitsLeft == 0) mech.BreakShield();
        }

        // Three robot mini-bosses, spread round the arena on the far side from the player.
        private void SendRobots(FinaleRobots.Kind[] kinds)
        {
            Vector3 centre = transform.position;
            Vector3 fromPlayer = player != null ? centre - player.transform.position : Vector3.forward;
            fromPlayer.y = 0f;
            float baseAngle = Mathf.Atan2(fromPlayer.z, fromPlayer.x);
            for (int i = 0; i < kinds.Length; i++)
            {
                float a = baseAngle + (i - 1) * 1.1f;
                Vector3 desired = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 20f;
                Vector3 point = SpawnSafety.TryPlace(desired, 8f, out Vector3 safe, 24) ? safe : desired;
                Vector3 face = player != null ? player.transform.position - point : -fromPlayer;
                face.y = 0f;
                FinaleRobots.Robot robot = FinaleRobots.Spawn(kinds[i], robotSources, point, Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face : Vector3.forward), robotHealth);
                if (robot == null) continue;
                robots.Add(robot);
                ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, point + Vector3.up, Vector3.up, 80, 0.2f, 10f);
                ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, point, Vector3.up, 40, 2f, 3f);
            }
            Say($"Cavendish: Meet my improvements! {string.Join(", ", System.Array.ConvertAll(kinds, FinaleRobots.Name))}!", 3.5f);
        }

        private void OnConduitBroken(ShieldConduit conduit)
        {
            if (System.Array.IndexOf(task, conduit) < 0) return;
            ConduitsLeft = 0;
            foreach (ShieldConduit c in task)
                if (c != null && c.Live) ConduitsLeft++;
            UpdateTaskText();
            if (ConduitsLeft > 0) return;
            Say("Cavendish: No, no, NO! My shield!", 2.5f);
            mech.BreakShield();
        }

        private void UpdateTaskText()
        {
            string placeholder = task == phaseTwoConduits ? "  [PLACEHOLDER: the coolant-cell task isn't built yet]" : "";
            GameplayOverlay.Instance.SetQuest("Shield Up!", $"The mech is shielded. Find the power conduits with the scanner (Q) and break them: " +
                $"{ConduitsTotal - ConduitsLeft} / {ConduitsTotal}.{placeholder}");
        }

        // Robot creatures all fight long: the gap between spawns shrinks and the cap on how many are alive grows with
        // the minutes fought and the phase, and both jump while the shield is up.
        private IEnumerator Horde()
        {
            while (Current == State.Fighting)
            {
                float minutes = FightTime / 60f;
                int phase = mech.Phase;
                bool shielded = mech.Shielded;
                float gap = Mathf.Max(hordeFastest, hordeInterval - minutes * 0.5f - (phase - 1) - (shielded ? 1f : 0f));
                int batch = Mathf.Clamp(1 + Mathf.FloorToInt(minutes / 2f) + (shielded ? 1 : 0), 1, 4);
                for (int i = 0; i < batch && Adds.Count < HordeCap; i++) SpawnAdd();
                yield return new WaitForSeconds(gap);
            }
        }

        private void SpawnAdd()
        {
            if (addPrefabs.Length == 0 || player == null) return;
            GameObject prefab = addPrefabs[Random.Range(0, addPrefabs.Length)];
            if (prefab == null) return;
            Vector3 centre = transform.position;
            Vector3 fromCentre = player.transform.position - centre;
            fromCentre.y = 0f;
            float angle = Mathf.Atan2(fromCentre.z, fromCentre.x) + Random.Range(-1.2f, 1.2f);
            Vector3 desired = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(18f, 28f);
            if (!SpawnSafety.TryPlace(desired, 6f, out Vector3 point)) return;
            Vector3 face = player.transform.position - point;
            face.y = 0f;
            GameObject spawned = Instantiate(prefab, point, Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face : Vector3.forward));
            spawned.name = "Robo " + prefab.name;
            spawned.AddComponent<Robotify>().addParts = spawned.GetComponentInChildren<Renderer>() != null;
            if (arena != null)
                foreach (ITerritorial enemy in spawned.GetComponentsInChildren<ITerritorial>()) enemy.Territory.Zone = arena;
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, point, Vector3.up, 30, 0.6f, 5f);
            Adds.Add(new Add { Go = spawned, Health = spawned.GetComponent<Health>() });
        }

        // ---------- Retries ----------

        private int retryPhase;

        private void OnPlayerDied(Health _)
        {
            if (Current != State.Fighting) return;
            retryPhase = mech.Phase;
        }

        private void OnPlayerRespawned()
        {
            if (Current != State.Fighting) return;
            Retries++;
            if (adds != null) StopCoroutine(adds);
            foreach (Add add in Adds)
                if (add.Go != null) Destroy(add.Go);
            Adds.Clear();
            Crew.ResetCall();
            foreach (FinaleRobots.Robot robot in robots)
                if (robot.Go != null) Destroy(robot.Go);
            robots.Clear();
            foreach (ShieldConduit c in conduits) c.ResetConduit();
            foreach (ShieldConduit c in phaseTwoConduits) c.ResetConduit();
            // Conduits of finished phases stay broken.
            if (retryPhase >= 2) foreach (ShieldConduit c in conduits) c.SetLive(false);
            mech.ResetToPhase(retryPhase);
            Say(RetryLines[retryLine++ % RetryLines.Length], 3f);
            GameplayOverlay.Instance.SetQuest("Safe Keeping", $"Back to phase {retryPhase}. Catch your breath; the fight resumes in a moment.");
            StartCoroutine(ResumeSoon());
        }

        private IEnumerator ResumeSoon()
        {
            yield return new WaitForSeconds(3f);
            mech.Resume();
            fightStart = Time.time; // the horde builds up again from the retry
            adds = StartCoroutine(Horde());
        }

        // ---------- The end ----------

        private void OnBeaten()
        {
            Current = State.Won;
            if (adds != null) StopCoroutine(adds);
            // With their maker down, his robots power off.
            foreach (Add add in Adds)
                if (add.Health != null && !add.Health.IsDead) add.Health.TakeDamage(add.Health.Max * 10f, gameObject);
            foreach (FinaleRobots.Robot robot in robots)
                if (robot.Alive) robot.Health.TakeDamage(robot.Health.Max * 10f, gameObject);
            Crew.Enabled = false;
            StageProgress.RecoverMainPart("Reactor Core");
            StageProgress.ClearStage(StageProgress.Finale, null);
            Say("Cavendish: No... my ship... my beautiful ship...", 4f);
            GameplayOverlay.Instance.SetQuest("Safe Keeping", "Cavendish's mech is down!  [PLACEHOLDER: the meltdown, the escape run and the launch aren't built yet.]", true);
            MiniBossHud.Message("THE SAFEKEEPER IS DOWN!", new Color(1f, 0.85f, 0.35f), 5f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, triggerRadius);
        }
    }
}
