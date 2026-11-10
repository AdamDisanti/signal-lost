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
        [Tooltip("Creatures sent in while the shield is up (any enemy prefab; swap in each island's own as they're built).")]
        [SerializeField] private GameObject[] addPrefabs = new GameObject[0];
        [SerializeField, Range(0, 3)] private int addWaves = 2;
        [SerializeField, Range(1, 8)] private int addsPerWave = 3;
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

        private PlayerLife player;
        private ShieldConduit[] task = new ShieldConduit[0];
        private Coroutine adds;
        private int retryLine;

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
            Current = State.Fighting;
            Crew.Enabled = true;
        }

        /// <summary>Skips the intro's dialogue (for tests).</summary>
        public void SkipIntro()
        {
            if (Current != State.Waiting && Current != State.Intro) return;
            StopAllCoroutines();
            Current = State.Fighting;
            Crew.Enabled = true;
            mech.Wake();
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
            if (ConduitsLeft == 0) { mech.BreakShield(); return; }
            if (adds != null) StopCoroutine(adds);
            adds = StartCoroutine(SendAdds());
        }

        private void OnConduitBroken(ShieldConduit conduit)
        {
            if (System.Array.IndexOf(task, conduit) < 0) return;
            ConduitsLeft = 0;
            foreach (ShieldConduit c in task)
                if (c != null && c.Live) ConduitsLeft++;
            UpdateTaskText();
            if (ConduitsLeft > 0) return;
            if (adds != null) StopCoroutine(adds);
            Say("Cavendish: No, no, NO! My shield!", 2.5f);
            mech.BreakShield();
        }

        private void UpdateTaskText()
        {
            string placeholder = task == phaseTwoConduits ? "  [PLACEHOLDER: the coolant-cell task isn't built yet]" : "";
            GameplayOverlay.Instance.SetQuest("Shield Up!", $"The mech is shielded. Find the power conduits with the scanner (Q) and break them: " +
                $"{ConduitsTotal - ConduitsLeft} / {ConduitsTotal}.{placeholder}");
        }

        private IEnumerator SendAdds()
        {
            for (int wave = 0; wave < addWaves; wave++)
            {
                yield return new WaitForSeconds(wave == 0 ? 3f : 14f);
                if (!mech.Shielded) yield break;
                for (int i = 0; i < addsPerWave; i++) SpawnAdd();
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
            spawned.name = prefab.name + " (add)";
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
        }

        // ---------- The end ----------

        private void OnBeaten()
        {
            Current = State.Won;
            if (adds != null) StopCoroutine(adds);
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
