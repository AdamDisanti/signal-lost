using System.Collections;
using SignalLost.Effects;
using SignalLost.Enemies.Desert;
using SignalLost.Mudlands;
using SignalLost.Player;
using SignalLost.Scanner;
using UnityEngine;

namespace SignalLost.Enemies.Caves
{
    /// <summary>
    /// The Cave Axolotl's deep chamber: the stage-boss flow, the Crystal Caves' version of the Core Worm's
    /// <see cref="DesertStageExit"/> (docs/cave-axolotl.md).
    ///
    /// 1. The axolotl waits under a crystal in the middle of its chamber, switched off.
    /// 2. Walk into the chamber (within Trigger Radius of this object) with all three cave mini-boss parts (the Shard Mole's
    ///    antenna array, the Beetles' signal amplifier and the Geode Bear's power cell) and the Crystal Disruptor scanner upgrade, and the entrance plays: the cave rumbles, the crystal
    ///    shakes, it bursts out and roars, and its name card shows. Space skips (the shared <see cref="BossEntrance"/>).
    /// 3. Die, or run out of the chamber, and it resets and goes back under its crystal; walk in again to retry.
    /// 4. Beat it and pick up the Flight Control Module: the Crystal Caves is cleared (EngineCorePickup records it).
    /// </summary>
    public sealed class AxolotlLair : MonoBehaviour
    {
        private enum Stage { Waiting, Cutscene, Fighting, Cleared }

        [SerializeField] private CaveAxolotl axolotl;
        [SerializeField, Min(1f)] private float triggerRadius = 6f;
        [Tooltip("Cave mini-boss parts needed first (the antenna array, the signal amplifier and the power cell).")]
        [SerializeField, Min(0)] private int requiredParts = 3;
        [Tooltip("Testing only: act as if the parts were already collected.")]
        [SerializeField] private bool assumePartsCollected;
        [SerializeField] private string title = "THE CAVE AXOLOTL";
        [SerializeField] private string subtitle = "It's guarding the ship's Flight Control Module";
        [Header("Chamber chameleons")]
        [Tooltip("Hidden in the chamber's floor; they rise on their own when phase 2 starts, a few at a time. The scanner never sees them.")]
        [SerializeField] private CrystalChameleon[] chamberChameleons = new CrystalChameleon[0];
        [SerializeField, Min(1)] private int risePerWave = 2;
        [SerializeField, Min(0.5f)] private float riseEvery = 4f;

        private Stage stage = Stage.Waiting;
        private Transform player;
        private ScannerController scanner;
        private float nextNag;
        private bool chameleonsRising;

        /// <summary>Counts the parts that open the chamber. Unset (the test scene): the parts the player has picked up.
        /// The level's story sets it to the parts handed to Cavendish.</summary>
        public System.Func<int> PartCount { get; set; }

        public bool InCutsceneOrFight => stage == Stage.Cutscene || stage == Stage.Fighting;
        public bool Cleared => stage == Stage.Cleared;

        private void Start()
        {
            var controller = FindAnyObjectByType<FirstPersonController>();
            if (controller != null)
            {
                player = controller.transform;
                scanner = controller.GetComponentInChildren<ScannerController>(true);
            }
            if (axolotl != null) axolotl.Lurk();
            EngineCorePickup.Recovered += OnPartRecovered;
        }

        private void OnDestroy() => EngineCorePickup.Recovered -= OnPartRecovered;

        private void Update()
        {
            if (axolotl == null || player == null) return;
            if (stage == Stage.Fighting && !axolotl.IsDead && !axolotl.Engaged)
            {
                // It reset (the player died or ran off): back under its crystal until they come in again.
                stage = Stage.Waiting;
                axolotl.Lurk();
                MiniBossHud.Message("The Cave Axolotl sank back under its crystal...", new Color(1f, 0.6f, 0.8f), 4f);
                return;
            }
            if (stage == Stage.Fighting && !chameleonsRising && axolotl.DuelPhase >= 2) StartCoroutine(RaiseChameleons());
            if (stage != Stage.Waiting) return;
            Vector3 offset = player.position - transform.position;
            offset.y = 0f;
            if (offset.magnitude > triggerRadius) return;

            int have = PartCount != null ? PartCount() : ShipPartDrop.CountFor(CaveAxolotl.Stage);
            bool partsMissing = !assumePartsCollected && have < requiredParts;
            // Without the Crystal Disruptor the player could never find it once it hides (Amanda, 11-12).
            bool disruptorMissing = scanner == null || !scanner.DisruptsCamouflage;
            if (partsMissing || disruptorMissing)
            {
                if (Time.time < nextNag) return;
                nextNag = Time.time + 4f;
                string parts = PartCount != null
                    ? $"Cavendish needs all three ship parts before you take this on ({have}/{requiredParts} handed in)."
                    : $"Something big is sleeping down here. Find the other ship parts first ({have}/{requiredParts}).";
                const string disruptor = "One of these crystals is alive, but your scanner can't see through it. Eugene sells the Crystal Disruptor upgrade.";
                if (partsMissing && disruptorMissing) MiniBossHud.Message(parts, new Color(1f, 0.85f, 0.5f), 4f, disruptor);
                else MiniBossHud.Message(partsMissing ? parts : disruptor, new Color(1f, 0.85f, 0.5f), 4f);
                return;
            }
            StartCoroutine(Entrance());
        }

        private IEnumerator Entrance()
        {
            stage = Stage.Cutscene;
            // The car can reach the caves (the Mudlands is sailed first since 11-22): park it for the entrance.
            if (SignalLost.Mudlands.DrunkDriving.Instance != null) SignalLost.Mudlands.DrunkDriving.Instance.ForceEnd(false);
            var shot = new BossEntrance(this);
            shot.Begin();
            Vector3 lair = axolotl.DisguisePosition;
            Vector3 eye = player.position + Vector3.up * 1.7f;
            Vector3 away = player.position - lair;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back;
            shot.Cut(eye, lair + Vector3.up);
            CorewormHud.Subtitle("One of the crystals is moving...", 2f);

            // The cave rumbles and the crystal shakes loose dust as the camera creeps in.
            StartCoroutine(Rumble(lair, 1.8f));
            yield return shot.Move(lair + away * 11f + Vector3.up * 3.5f, lair + Vector3.up * 1.5f, 1.8f);

            // It bursts out of its crystal and roars (the intro is its first move).
            axolotl.enabled = true;
            while (!axolotl.Started) yield return null;
            axolotl.BeginFight();
            shot.LookAt(axolotl.transform, Vector3.up * 2f);
            yield return shot.Wait(0.9f);
            if (!shot.Skipped) CorewormHud.NameCard(title, subtitle, 3.2f);
            while (axolotl.CurrentMove == "Intro" && !shot.Tick()) yield return null;
            shot.End();
            stage = Stage.Fighting;
        }

        // Phase 2 (Crystal Bloom): every chamber chameleon rises, a few at a time, however fast the fight moves on.
        private IEnumerator RaiseChameleons()
        {
            chameleonsRising = true;
            int risen = 0;
            foreach (CrystalChameleon chameleon in chamberChameleons)
            {
                if (chameleon == null || !chameleon.IsDisguised) continue;
                chameleon.Disrupt();
                if (++risen % risePerWave == 0) yield return new WaitForSeconds(riseEvery);
            }
        }

        private static IEnumerator Rumble(Vector3 at, float seconds)
        {
            float next = 0f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                CameraShake.Add(Time.deltaTime * (0.2f + t * 0.3f));
                if (Time.time >= next)
                {
                    next = Time.time + 0.15f;
                    Vector3 p = at + Random.insideUnitSphere * 2.5f;
                    p.y = at.y + 0.2f;
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sand, p, Vector3.up, 4, 0.7f, 2.5f, 40f, new Color(0.42f, 0.43f, 0.5f));
                }
                yield return null;
            }
        }

        private void OnPartRecovered(string part)
        {
            if (part != CaveAxolotl.MainPart) return;
            stage = Stage.Cleared;
            CorewormHud.Subtitle("The Flight Control Module. The Crystal Caves is clear.", 4f);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.8f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, triggerRadius);
        }
    }
}
