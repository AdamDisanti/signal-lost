using System.Collections.Generic;

namespace SignalLost.Progression
{
    // Save and load (Christian, 10-27): the save-only part of StageProgress. The rest is in Progression/StageProgress.cs.
    public static partial class StageProgress
    {
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
    }
}
