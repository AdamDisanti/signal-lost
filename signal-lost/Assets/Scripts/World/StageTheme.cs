using UnityEngine;

namespace SignalLost.World
{
    /// <summary>The look of each island, which decides what the mechanic, Cavendish and Eugene wear there.</summary>
    public enum IslandLook { Meadow, Desert }

    /// <summary>
    /// Says which island this scene is (11-08), so the cast dresses for it (<see cref="Outfits"/>). One per scene;
    /// a scene without one is Crash Meadow, the look everyone starts the game in.
    /// </summary>
    public sealed class StageTheme : MonoBehaviour
    {
        [SerializeField] private IslandLook look = IslandLook.Meadow;

        private static StageTheme current;

        /// <summary>
        /// This scene's look. Found on demand rather than in Awake, so other components can ask from their own Awake
        /// whatever order the scene wakes up in.
        /// </summary>
        public static IslandLook Current
        {
            get
            {
                if (current == null) current = FindAnyObjectByType<StageTheme>();
                return current != null ? current.look : IslandLook.Meadow;
            }
        }

        public IslandLook Look => look;
    }
}
