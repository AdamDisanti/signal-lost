using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// The Dish Meerkat's "tag": when one shrieks at the player, the player is spotted for a few seconds. While
    /// spotted, every desert fodder enemy within its alerted range knows where the player is (no line of sight
    /// needed) and moves faster. Mini-bosses can read <see cref="IsMarked"/> later, for example to target the player.
    ///
    /// This is a static service so any script can use it without scene setup. Marking creates a small on-screen
    /// readout (<see cref="FodderStatusOverlay"/>) the first time it is needed.
    /// </summary>
    public static class SpotterAlert
    {
        private static float markedUntil = float.NegativeInfinity;

        /// <summary>True while the player is spotted.</summary>
        public static bool IsMarked => Time.time < markedUntil;

        /// <summary>Seconds of being spotted left (0 when not spotted).</summary>
        public static float TimeLeft => Mathf.Max(0f, markedUntil - Time.time);

        /// <summary>Where the meerkat that spotted the player last shrieked from.</summary>
        public static Vector3 LastSpotter { get; private set; }

        /// <summary>Spots the player for at least <paramref name="seconds"/> (refreshes, never shortens).</summary>
        public static void Mark(float seconds, Vector3 spotterPosition)
        {
            markedUntil = Mathf.Max(markedUntil, Time.time + seconds);
            LastSpotter = spotterPosition;
            FodderStatusOverlay.Ensure();
        }

        /// <summary>Ends the spotted state, for example when the player respawns.</summary>
        public static void Clear() => markedUntil = float.NegativeInfinity;

        // Static state survives between Play Mode sessions when domain reload is off; reset it on each start.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => markedUntil = float.NegativeInfinity;
    }
}
