using System.Collections.Generic;
using SignalLost.Progression;
using UnityEngine;

namespace SignalLost.World
{
    /// <summary>
    /// A dock on a stage's island (11-07). A <b>departure</b> dock sits at the end of a stage: its moored boat
    /// (<see cref="DockBoat"/>) sails the player to the next stage's island, in the <see cref="IslandVoyage"/> cutscene,
    /// once <see cref="RequiredStage"/> is cleared. An <b>arrival</b> dock is where that voyage lands on the next island:
    /// the cutscene sails its boat in and leaves the player standing at <see cref="Landing"/>.
    /// Built by the level builders (IslandKit); the transforms are children of the dock.
    /// </summary>
    public sealed class IslandDock : MonoBehaviour
    {
        public enum Role { Departure, Arrival }

        private static readonly List<IslandDock> All = new List<IslandDock>();

        [SerializeField] private Role role = Role.Departure;
        [Tooltip("This island's name, shown when arriving.")]
        [SerializeField] private string islandName = "";
        [SerializeField] private Transform boat;
        [Tooltip("Where the boat is tied up.")]
        [SerializeField] private Transform mooring;
        [Tooltip("Where the player stands on the dock after landing.")]
        [SerializeField] private Transform landing;

        [Header("Departure")]
        [Tooltip("Scene loaded at the end of the voyage. Must be in the build's scene list.")]
        [SerializeField] private string destinationScene = "";
        [SerializeField] private string destinationName = "the next island";
        [Tooltip("The boat only sails once this stage is cleared (StageProgress). Empty: always.")]
        [SerializeField] private string requiredStage = "";
        [SerializeField] private string lockedMessage = "The boat isn't ready to sail yet.";

        public Role DockRole => role;
        public string IslandName => islandName;
        public Transform Boat => boat;
        public Transform Mooring => mooring != null ? mooring : transform;
        public Transform Landing => landing != null ? landing : transform;
        public string DestinationScene => destinationScene;
        public string DestinationName => destinationName;
        public string RequiredStage => requiredStage;
        public string LockedMessage => lockedMessage;

        /// <summary>Straight out to sea from the end of the dock (the dock's forward, flattened).</summary>
        public Vector3 Seaward
        {
            get
            {
                Vector3 forward = transform.forward;
                forward.y = 0f;
                return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            }
        }

        /// <summary>Testing: lets the boat sail without clearing the stage.</summary>
        public static bool IgnoreRequirements { get; set; }

        public bool IsOpen => role == Role.Departure &&
                              (IgnoreRequirements || string.IsNullOrEmpty(requiredStage) || StageProgress.IsCleared(requiredStage));

        public bool DestinationExists => !string.IsNullOrEmpty(destinationScene) && Application.CanStreamedLevelBeLoaded(destinationScene);

        public static IslandDock Find(Role role)
        {
            foreach (IslandDock dock in All)
                if (dock != null && dock.role == role) return dock;
            return null;
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.color = role == Role.Departure ? new Color(1f, 0.6f, 0.2f) : new Color(0.3f, 0.9f, 0.6f);
            Gizmos.DrawLine(Mooring.position, Mooring.position + Seaward * 12f);
            Gizmos.DrawWireSphere(Landing.position + Vector3.up * 0.9f, 0.45f);
        }
    }
}
