using System;
using System.Collections.Generic;
using SignalLost.Enemies.Desert;
using UnityEngine;

namespace SignalLost.Progression
{
    /// <summary>
    /// PLACEHOLDER progression for the current Play session: which stages are cleared and unlocked, and the
    /// main ship parts recovered. It resets when Play stops; save slots keep a copy (<see cref="SaveGame"/>, through
    /// <see cref="Capture"/> and <see cref="Restore"/>). Death persistence is still open (see docs/design.md).
    ///
    /// Mini-boss parts are tracked by <see cref="ShipPartDrop"/>, per stage (the desert's three; the tutorial's cooling
    /// system from the Chief Toad); the Engine Core, one of the main parts needed to finish the game, is recorded here
    /// when the desert stage boss drops it.
    /// </summary>
    public static class StageProgress
    {
        public const string Desert = "Desert";
        public const string Tutorial = "Tutorial";

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
        public static int StagePartsRecovered => ShipPartDrop.CountFor(Desert);

        public static void RecoverMainPart(string part)
        {
            if (MainParts.Contains(part)) return;
            MainParts.Add(part);
            MainPartRecovered?.Invoke(part);
        }

        /// <summary>Copies the progress into a save slot's lists.</summary>
        public static void Capture(List<string> cleared, List<string> unlocked, List<string> mainParts)
        {
            cleared.AddRange(Cleared);
            unlocked.AddRange(Unlocked);
            mainParts.AddRange(MainParts);
        }

        /// <summary>Loading a save slot (or New Game, with nulls): replaces the progress quietly, without events.</summary>
        public static void Restore(IEnumerable<string> cleared, IEnumerable<string> unlocked, IEnumerable<string> mainParts)
        {
            Cleared.Clear();
            Unlocked.Clear();
            MainParts.Clear();
            if (cleared != null) Cleared.UnionWith(cleared);
            if (unlocked != null) Unlocked.UnionWith(unlocked);
            if (mainParts != null) foreach (string part in mainParts) if (!MainParts.Contains(part)) MainParts.Add(part);
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
