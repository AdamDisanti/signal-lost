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
    /// Mini-boss parts are tracked by <see cref="ShipPartDrop"/>, per stage (the desert's three; the tutorial's cooling
    /// system from the Chief Toad); the Engine Core, one of the main parts needed to finish the game, is recorded here
    /// when the desert stage boss drops it.
    ///
    /// The islands are sailed in this order (11-22): Crash Meadow (the tutorial), the Mudlands, the Desert Dunes, the
    /// Crystal Caves, then the Wasteland (the finale). <see cref="StageSkip"/> lists the same order; the docks are linked
    /// by the level builders and by Signal Lost &gt; Islands &gt; Link Stage Order.
    /// </summary>
    public static class StageProgress
    {
        public const string Tutorial = "Tutorial";
        /// <summary>The second stage since 11-22 (the user: the car is earned here, so it is there on every stage after).</summary>
        public const string Mudlands = "Mudlands";
        public const string Desert = "Desert";
        public const string CrystalCaves = "CrystalCaves";
        /// <summary>The final level: the crash-site island laid waste, and Cavendish's mech (docs/final-boss.md).</summary>
        public const string Finale = "Finale";

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

        /// <summary>Marks a stage cleared and unlocks the one after it (which may not exist yet).</summary>
        public static void ClearStage(string stage, string unlocks)
        {
            Cleared.Add(stage);
            if (string.IsNullOrEmpty(unlocks) || !Unlocked.Add(unlocks)) return;
            StageUnlocked?.Invoke(unlocks);
        }
    }
}
