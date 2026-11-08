using SignalLost.Effects;
using SignalLost.Enemies.Desert;
using SignalLost.Player;
using UnityEngine;

namespace SignalLost.World
{
    /// <summary>
    /// The boat moored at an <see cref="IslandDock"/>. It rocks gently on the water. At a departure dock, looking at it
    /// and pressing E starts the <see cref="IslandVoyage"/> to the next island, or says why it can't sail yet.
    /// While a voyage is moving it, the voyage drives its transform and it stops rocking.
    /// </summary>
    public sealed class DockBoat : MonoBehaviour, IInteractable
    {
        [SerializeField] private IslandDock dock;
        [SerializeField, Min(0f)] private float bobHeight = 0.08f;
        [SerializeField, Min(0f)] private float rollDegrees = 2.2f;
        [Tooltip("The wake trail at the stern: it only draws while sailing.")]
        [SerializeField] private TrailRenderer wake;

        private Vector3 restPosition;
        private Quaternion restRotation;
        private float phase;

        /// <summary>Set by the voyage while it moves the boat.</summary>
        public bool Sailing { get; set; }

        public IslandDock Dock => dock;
        public TrailRenderer Wake => wake;

        private void Awake()
        {
            restPosition = transform.position;
            restRotation = transform.rotation;
            phase = Random.value * 10f;
            if (wake != null)
            {
                wake.emitting = false;
                if (wake.sharedMaterial == null) wake.sharedMaterial = EffectMaterials.AlphaBlended(EffectMaterials.Look.Beam);
            }
        }

        private void Update()
        {
            if (Sailing) return;
            float t = Time.time + phase;
            transform.SetPositionAndRotation(restPosition + Vector3.up * (Mathf.Sin(t * 1.3f) * bobHeight),
                restRotation * Quaternion.Euler(Mathf.Sin(t * 0.9f) * rollDegrees * 0.5f, 0f, Mathf.Sin(t * 1.1f + 1f) * rollDegrees));
        }

        /// <summary>The gentle rocking at time <paramref name="t"/>, for the voyage to layer onto the boat's course.</summary>
        public Quaternion Rock(float t) => Quaternion.Euler(Mathf.Sin(t * 0.9f) * rollDegrees, 0f, Mathf.Sin(t * 1.1f + 1f) * rollDegrees * 1.5f);
        public float Bob(float t) => Mathf.Sin(t * 1.3f) * bobHeight * 1.5f;

        public string Prompt
        {
            get
            {
                if (dock == null || dock.DockRole != IslandDock.Role.Departure) return "";
                if (!dock.IsOpen) return "Boat (not ready to sail)";
                return "Sail to " + dock.DestinationName;
            }
        }

        public bool CanInteract(PlayerInteractor player) =>
            dock != null && dock.DockRole == IslandDock.Role.Departure && !Sailing && !IslandVoyage.InProgress;

        public bool Interact(PlayerInteractor player)
        {
            if (!CanInteract(player)) return false;
            if (!dock.IsOpen)
            {
                MiniBossHud.Message(dock.LockedMessage, new Color(1f, 0.8f, 0.45f), 3f);
                return true;
            }
            if (!dock.DestinationExists)
            {
                MiniBossHud.Message($"{dock.DestinationName} hasn't been built yet.", new Color(0.55f, 1f, 0.6f), 3.5f);
                return true;
            }
            return IslandVoyage.Begin(dock);
        }
    }
}
