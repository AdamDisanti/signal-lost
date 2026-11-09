using System;
using SignalLost.Effects;
using SignalLost.Player;
using SignalLost.Scanner;
using UnityEngine;

namespace SignalLost.Story
{
    /// <summary>
    /// One small job in a quest, done with E while looking at it: pick up a feather, smother a fire, seal a leak
    /// (the desert's quests, 11-08). It only responds while the story has it <see cref="Armed"/>; then its quest signal
    /// is on, so the scanner leads to it. Doing it puffs some dust, puts out its fires and particles, hides what should
    /// go (the feather, the spray) and shows what should appear (a patch of sand, a clamp), then raises <see cref="Done"/>.
    /// </summary>
    public sealed class QuestTask : MonoBehaviour, IInteractable
    {
        [Tooltip("Shown under the crosshair, e.g. \"Smother the fire\".")]
        [SerializeField] private string prompt = "Do it";
        [Tooltip("A short line shown when it is done.")]
        [SerializeField] private string doneMessage = "";
        [SerializeField] private ParticleBurst.Kind puff = ParticleBurst.Kind.Sand;
        [Tooltip("Hidden once done (the feather, the oil spray).")]
        [SerializeField] private GameObject[] hideWhenDone = Array.Empty<GameObject>();
        [Tooltip("Shown once done (a sand patch, a clamp).")]
        [SerializeField] private GameObject[] showWhenDone = Array.Empty<GameObject>();

        private QuestSignalSource signal;
        private bool armed;

        /// <summary>Raised when the player does it.</summary>
        public event Action<QuestTask> Done;

        public bool IsDone { get; private set; }
        public string DoneMessage => doneMessage;
        public string Prompt => armed && !IsDone ? prompt : "";

        /// <summary>Only an armed task can be done, and only an armed one shows on the scanner.</summary>
        public bool Armed
        {
            get => armed;
            set
            {
                armed = value;
                Refresh();
            }
        }

        private void Awake()
        {
            signal = GetComponent<QuestSignalSource>();
            foreach (GameObject item in showWhenDone) if (item != null) item.SetActive(false);
            Refresh();
        }

        public bool CanInteract(PlayerInteractor player) => isActiveAndEnabled && armed && !IsDone;

        public bool Interact(PlayerInteractor player)
        {
            if (!CanInteract(player)) return false;
            Complete();
            return true;
        }

        /// <summary>Does the job as if the player had pressed E (also used by tests).</summary>
        public void Complete()
        {
            if (IsDone) return;
            IsDone = true;
            Vector3 at = transform.position + Vector3.up * 0.4f;
            ParticleBurst.Spawn(puff, at, Vector3.up, 18, 0.8f, 3.5f, 70f);
            foreach (FireEffect fire in GetComponentsInChildren<FireEffect>()) fire.Extinguish();
            foreach (ParticleSystem system in GetComponentsInChildren<ParticleSystem>())
                if (system.GetComponentInParent<FireEffect>() == null) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            foreach (Light light in GetComponentsInChildren<Light>()) if (light.GetComponentInParent<FireEffect>() == null) light.enabled = false;
            foreach (GameObject item in hideWhenDone) if (item != null) item.SetActive(false);
            foreach (GameObject item in showWhenDone) if (item != null) item.SetActive(true);
            Refresh();
            Done?.Invoke(this);
        }

        private void Refresh()
        {
            if (signal != null) signal.enabled = armed && !IsDone;
        }
    }
}
