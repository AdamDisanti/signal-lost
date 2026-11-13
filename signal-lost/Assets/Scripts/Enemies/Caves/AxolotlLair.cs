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
    /// 2. Walk into the chamber (within Trigger Radius of this object) with both cave mini-boss parts (the Shard Mole's
    ///    antenna array and the Beetles' signal amplifier) and the Crystal Disruptor scanner upgrade, and the entrance plays: the cave rumbles, the crystal
    ///    shakes, it bursts out and roars, and its name card shows. Space skips (the shared <see cref="BossEntrance"/>).
    /// 3. Die, or run out of the chamber, and it resets and goes back under its crystal; walk in again to retry.
    /// 4. Beat it and pick up the Flight Control Module: the Crystal Caves is cleared (EngineCorePickup records it).
    /// </summary>
    public sealed class AxolotlLair : MonoBehaviour
    {
        private enum Stage { Waiting, Cutscene, Fighting, Cleared }

        [SerializeField] private CaveAxolotl axolotl;
        [SerializeField, Min(1f)] private float triggerRadius = 6f;
        [Tooltip("Cave mini-boss parts needed first (the antenna array and the signal amplifier).")]
        [SerializeField, Min(0)] private int requiredParts = 2;
        [Tooltip("Testing only: act as if the parts were already collected.")]
        [SerializeField] private bool assumePartsCollected;
        [SerializeField] private string title = "THE CAVE AXOLOTL";
        [SerializeField] private string subtitle = "It's guarding the ship's Flight Control Module";

        private Stage stage = Stage.Waiting;
        private Transform player;
        private ScannerController scanner;
        private float nextNag;

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
            if (stage != Stage.Waiting) return;
            Vector3 offset = player.position - transform.position;
            offset.y = 0f;
            if (offset.magnitude > triggerRadius) return;

            int have = ShipPartDrop.CountFor(CaveAxolotl.Stage);
            if (!assumePartsCollected && have < requiredParts)
            {
                if (Time.time < nextNag) return;
                nextNag = Time.time + 4f;
                MiniBossHud.Message($"Something big is sleeping down here. Find the other ship parts first ({have}/{requiredParts}).",
                    new Color(1f, 0.85f, 0.5f), 3f);
                return;
            }
            // Without the Crystal Disruptor the player could never find it once it hides (Amanda, 11-12).
            if (scanner == null || !scanner.DisruptsCamouflage)
            {
                if (Time.time < nextNag) return;
                nextNag = Time.time + 4f;
                MiniBossHud.Message("One of these crystals is alive, but your scanner can't see through it.\nEugene sells the Crystal Disruptor upgrade.",
                    new Color(1f, 0.85f, 0.5f), 4f);
                return;
            }
            StartCoroutine(Entrance());
        }

        private IEnumerator Entrance()
        {
            stage = Stage.Cutscene;
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
