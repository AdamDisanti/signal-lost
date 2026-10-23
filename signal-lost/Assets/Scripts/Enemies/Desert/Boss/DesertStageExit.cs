using System.Collections;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// The way out of the desert stage, and the Coreworm ambush.
    ///
    /// 1. With all the stage's ship parts (the three mini-bosses'), walk to the exit to take them back to the ship.
    /// 2. Cutscene: the ground rumbles, the Coreworm rockets out of the sand right where you were about to leave, roars,
    ///    and swallows you; you are dragged under the dunes into its arena, where it bursts out again. The fight starts.
    /// 3. If you die, you respawn at the stage start and the worm waits under the exit again.
    /// 4. Beat it and pick up the Engine Core: you are brought back to the exit, which now leaves the stage and
    ///    unlocks the next one. The next stage doesn't exist yet: set Next Stage Scene once it does.
    /// The player is given the Scotch flask (<see cref="PlayerGear"/>, F to drink via <see cref="PlayerFlask"/>) when the
    /// fight starts, if they don't have it yet. During the cutscenes their movement is off and their tools holstered.
    /// </summary>
    public sealed class DesertStageExit : MonoBehaviour
    {
        private enum Stage { Waiting, Cutscene, Fighting, Defeated, Cleared }

        [SerializeField] private Coreworm worm;
        [SerializeField] private CorewormArena arena;
        [Tooltip("Where the worm bursts out, a few metres in front of the exit.")]
        [SerializeField] private Transform eruptPoint;
        [SerializeField, Min(1f)] private float triggerRadius = 4f;
        [Tooltip("Ship parts needed before leaving (the three mini-bosses' parts).")]
        [SerializeField, Min(0)] private int requiredParts = 3;
        [Tooltip("Testing only: act as if the parts were already collected.")]
        [SerializeField] private bool assumePartsCollected;
        [Tooltip("Where the player is brought back to after recovering the Engine Core.")]
        [SerializeField] private Transform returnPoint;
        [Tooltip("Scene loaded when leaving after the boss. Empty, or not in Build Settings: a placeholder message instead.")]
        [SerializeField] private string nextStageScene = "";
        [SerializeField] private string nextStageName = "NextStage";

        private Stage stage = Stage.Waiting;
        private Transform player;
        private CharacterController playerBody;
        private Health playerHealth;
        private FirstPersonController controller;
        private Camera playerCamera;
        private readonly List<Behaviour> controls = new List<Behaviour>();
        private Camera cutsceneCamera;
        private float nextNag;
        private PlayerGear gear;

        private void Start()
        {
            controller = FindAnyObjectByType<FirstPersonController>();
            if (controller != null)
            {
                player = controller.transform;
                playerBody = controller.GetComponent<CharacterController>();
                playerHealth = controller.GetComponent<Health>();
                playerCamera = controller.GetComponentInChildren<Camera>();
                gear = controller.GetComponent<PlayerGear>();
                controls.Add(controller);
                controls.Add(controller.GetComponent<PlayerFlask>());
                controls.Add(controller.GetComponent<PlayerInteractor>());
                if (gear == null) // an old test player: switch its tools off directly
                    foreach (Behaviour item in new Behaviour[] { controller.GetComponent<MiningLaser>(), controller.GetComponent<ScannerLaserSwap>() })
                        controls.Add(item);
                if (playerHealth != null) playerHealth.Died += OnPlayerDied;
            }
            if (worm != null)
            {
                worm.HideAt(EruptAt);
                worm.Defeated += OnWormDefeated;
            }
            EngineCorePickup.Recovered += OnCoreRecovered;
        }

        private void OnDestroy()
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
            if (worm != null) worm.Defeated -= OnWormDefeated;
            EngineCorePickup.Recovered -= OnCoreRecovered;
        }

        private Vector3 EruptAt => eruptPoint != null ? eruptPoint.position : transform.position;

        private void Update()
        {
            if (player == null || stage == Stage.Cutscene || stage == Stage.Fighting) return;
            Vector3 offset = player.position - transform.position;
            offset.y = 0f;
            if (offset.magnitude > triggerRadius) return;

            if (stage == Stage.Cleared)
            {
                LeaveStage();
                return;
            }
            if (stage == Stage.Defeated) return;
            int have = StageProgress.StagePartsRecovered;
            if (!assumePartsCollected && have < requiredParts)
            {
                if (Time.time < nextNag) return;
                nextNag = Time.time + 4f;
                MiniBossHud.Message($"You still need the ship parts the Chop Shop stole ({have}/{requiredParts}).", new Color(1f, 0.85f, 0.5f), 3f);
                return;
            }
            StartCoroutine(Ambush());
        }

        // ---------- The ambush cutscene ----------

        private IEnumerator Ambush()
        {
            stage = Stage.Cutscene;
            SetControls(false);
            BeginCutsceneCamera();
            CorewormHud.Subtitle("Time to get these parts back to the ship...", 2f);

            // The ground starts to shake around the exit.
            Vector3 erupt = EruptAt;
            float t = 0f, nextPuff = 0f;
            while (t < 1.8f)
            {
                t += Time.deltaTime;
                CameraShake.Add(Time.deltaTime * (0.2f + t * 0.3f));
                LookAt(erupt + Vector3.up * 2f, 2f);
                if (Time.time >= nextPuff)
                {
                    nextPuff = Time.time + 0.15f;
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sand, erupt + Flat(Random.insideUnitSphere) * 4f, Vector3.up, 4, 0.7f, 2.5f, 40f);
                }
                yield return null;
            }

            // It rockets out of the sand, right where you were about to leave.
            Vector3 eye = cutsceneCamera.transform.position;
            StartCoroutine(Follow(worm.Head, 5f, 2.3f));
            yield return worm.IntroErupt(erupt, 15f, eye);
            yield return worm.Roar(1.1f);

            // It lunges and swallows you.
            StartCoroutine(Follow(worm.Head, 12f, 0.5f));
            yield return worm.IntroLunge(eye + (worm.Head.position - eye).normalized * 1.2f, 0.45f);
            CorewormHud.FadeTo(1f, 0.12f, new Color(0.05f, 0.02f, 0.01f));
            CameraShake.Add(0.8f);
            yield return Wait(0.3f);

            // Dragged under the dunes to its arena.
            CorewormHud.Subtitle("Dragged beneath the dunes...", 2f);
            if (arena != null) arena.ResetArena();
            worm.HideAt(arena != null ? arena.Centre : erupt);
            Transform drop = arena != null ? arena.PlayerDrop : null;
            Vector3 landing = drop != null ? drop.position : erupt;
            Vector3 centre = arena != null ? arena.Centre : erupt;
            Teleport(landing, centre);
            yield return Wait(1.8f);

            // Spat out in the arena; it bursts out again and the fight begins.
            Vector3 view = landing + Vector3.up * 2.5f - Flat(centre - landing).normalized * 3f;
            cutsceneCamera.transform.SetPositionAndRotation(view, Quaternion.LookRotation(centre + Vector3.up * 6f - view));
            CorewormHud.FadeTo(0f, 0.6f);
            yield return Wait(0.6f);
            StartCoroutine(Follow(worm.Head, 4f, 3.6f));
            yield return worm.IntroErupt(centre, 16f, view);
            CorewormHud.NameCard("CORE WORM", "It swallowed the ship's Engine Core", 3.2f);
            yield return worm.Roar(1.6f);
            yield return worm.DiveAway(centre + Flat(landing - centre).normalized * 8f);

            EndCutsceneCamera();
            SetControls(true);
            controller.ResetMotion();
            stage = Stage.Fighting;
            if (gear != null && gear.Collect(PlayerItem.Flask))
                CorewormHud.Subtitle("You've still got the Scotch flask: press F to drink and heal.", 3.5f);
            worm.BeginFight();
        }

        // ---------- After the fight ----------

        private void OnPlayerDied(Health _)
        {
            if (stage != Stage.Fighting) return;
            // Respawning puts the player back at the stage start; the worm lies in wait under the exit again.
            worm.ResetFight(EruptAt);
            if (arena != null) arena.ResetArena();
            stage = Stage.Waiting;
            MiniBossHud.Message("The Coreworm got you. It's waiting at the exit...", new Color(1f, 0.6f, 0.5f), 4f);
        }

        private void OnWormDefeated()
        {
            stage = Stage.Defeated;
        }

        private void OnCoreRecovered(string part) => StartCoroutine(ReturnToExit());

        private IEnumerator ReturnToExit()
        {
            yield return Wait(2.5f);
            CorewormHud.FadeTo(1f, 0.8f);
            yield return Wait(0.9f);
            Vector3 at = returnPoint != null ? returnPoint.position : transform.position - transform.forward * 6f;
            Teleport(at, transform.position);
            controller.ResetMotion();
            CorewormHud.Subtitle("Back at the way out, with the Engine Core. The ship is waiting.", 3.5f);
            yield return Wait(0.4f);
            CorewormHud.FadeTo(0f, 0.8f);
            stage = Stage.Cleared;
        }

        private void LeaveStage()
        {
            stage = Stage.Cleared;
            if (!string.IsNullOrEmpty(nextStageScene) && Application.CanStreamedLevelBeLoaded(nextStageScene))
            {
                SceneManager.LoadScene(nextStageScene);
                return;
            }
            if (Time.time < nextNag) return;
            nextNag = Time.time + 5f;
            MiniBossHud.Message($"DESERT CLEARED!  {nextStageName} unlocked (it hasn't been built yet).", new Color(0.55f, 1f, 0.6f), 4f);
        }

        // ---------- Helpers ----------

        private void SetControls(bool on)
        {
            foreach (Behaviour item in controls) if (item != null) item.enabled = on;
            if (gear == null) return;
            if (on) gear.Unholster(this);
            else gear.Holster(this);
        }

        private void Teleport(Vector3 position, Vector3 lookAt)
        {
            if (player == null) return;
            if (playerBody != null) playerBody.enabled = false;
            Vector3 facing = Flat(lookAt - position);
            player.SetPositionAndRotation(position + Vector3.up * 0.1f, facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing) : player.rotation);
            if (playerBody != null) playerBody.enabled = true;
        }

        private void BeginCutsceneCamera()
        {
            if (cutsceneCamera == null)
            {
                var go = new GameObject("Cutscene Camera") { tag = "MainCamera" };
                cutsceneCamera = go.AddComponent<Camera>();
            }
            if (playerCamera != null)
            {
                cutsceneCamera.transform.SetPositionAndRotation(playerCamera.transform.position, playerCamera.transform.rotation);
                cutsceneCamera.fieldOfView = playerCamera.fieldOfView;
                cutsceneCamera.nearClipPlane = playerCamera.nearClipPlane;
                playerCamera.enabled = false;
            }
            cutsceneCamera.enabled = true;
        }

        private void EndCutsceneCamera()
        {
            if (cutsceneCamera != null) cutsceneCamera.enabled = false;
            if (playerCamera != null) playerCamera.enabled = true;
        }

        private void LookAt(Vector3 point, float rate)
        {
            if (cutsceneCamera == null) return;
            Transform view = cutsceneCamera.transform;
            Vector3 direction = point - view.position;
            if (direction.sqrMagnitude < 0.01f) return;
            view.rotation = Quaternion.Slerp(view.rotation, Quaternion.LookRotation(direction), 1f - Mathf.Exp(-rate * Time.deltaTime));
        }

        private IEnumerator Follow(Transform target, float rate, float time)
        {
            float t = 0f;
            while (t < time && target != null)
            {
                t += Time.deltaTime;
                LookAt(target.position, rate);
                yield return null;
            }
        }

        private static IEnumerator Wait(float time)
        {
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, triggerRadius);
            if (eruptPoint == null) return;
            Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(eruptPoint.position, 3f);
        }
    }
}
