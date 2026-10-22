using System;
using System.Collections.Generic;
using SignalLost.Enemies.Desert;
using UnityEngine;

namespace SignalLost.Progression
{
    /// <summary>
    /// PLACEHOLDER progression for the current Play session: which stages are cleared and unlocked, and the
    /// main ship parts recovered. There is no save system yet, so this resets when Play stops; the design for saving
    /// and death persistence is still open (see docs/design.md).
    ///
    /// The three desert mini-bosses' parts are tracked by <see cref="ShipPartDrop.Recovered"/>; the Engine Core, one of
    /// the main parts needed to finish the game, is recorded here when the desert stage boss drops it.
    /// </summary>
    public static class StageProgress
    {
        public const string Desert = "Desert";

        private static readonly HashSet<string> Cleared = new HashSet<string>();
        private static readonly HashSet<string> Unlocked = new HashSet<string>();
        private static readonly List<string> MainParts = new List<string>();

        /// <summary>Raised when a stage is unlocked, with the stage's name.</summary>
        public static event Action<string> StageUnlocked;

        /// <summary>Raised when a main ship part (like the Engine Core) is recovered.</summary>
        public static event Action<string> MainPartRecovered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cleared.Clear();
            Unlocked.Clear();
            MainParts.Clear();
            StageUnlocked = null;
            MainPartRecovered = null;
        }

        public static IReadOnlyList<string> MainShipParts => MainParts;
        public static bool HasMainPart(string part) => MainParts.Contains(part);
        public static bool IsCleared(string stage) => Cleared.Contains(stage);
        public static bool IsUnlocked(string stage) => Unlocked.Contains(stage);

        /// <summary>The desert mini-bosses' parts recovered so far this session.</summary>
        public static int StagePartsRecovered => ShipPartDrop.Recovered.Count;

        public static void RecoverMainPart(string part)
        {
            if (MainParts.Contains(part)) return;
            MainParts.Add(part);
            MainPartRecovered?.Invoke(part);
        }

        /// <summary>Marks a stage cleared and unlocks the one after it (which may not exist yet).</summary>
        public static void ClearStage(string stage, string unlocks)
        {
            Cleared.Add(stage);
            if (string.IsNullOrEmpty(unlocks) || !Unlocked.Add(unlocks)) return;
            StageUnlocked?.Invoke(unlocks);
        }
    }
}
