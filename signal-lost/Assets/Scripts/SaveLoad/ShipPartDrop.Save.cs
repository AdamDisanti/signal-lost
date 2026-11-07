using System.Collections.Generic;

namespace SignalLost.Enemies.Desert
{
    // Save and load (Christian, 10-27): the save-only part of ShipPartDrop. The rest is in
    // Enemies/Desert/MiniBosses/ShipPartDrop.cs.
    public sealed partial class ShipPartDrop
    {
        /// <summary>Copies the recovered parts (and the stage each belongs to) into a save slot's lists.</summary>
        public static void CaptureRecovered(List<string> names, List<string> stages)
        {
            names.AddRange(Recovered);
            stages.AddRange(RecoveredStages);
        }

        /// <summary>Loading a save slot (or New Game, with nulls): replaces the recovered parts quietly.</summary>
        public static void RestoreRecovered(IList<string> names, IList<string> stages)
        {
            Recovered.Clear();
            RecoveredStages.Clear();
            if (names == null || stages == null) return;
            for (int i = 0; i < names.Count && i < stages.Count; i++) RecordRecovered(names[i], stages[i]);
        }

        /// <summary>Counts a part as recovered without a pickup (a loaded save whose loot was never picked up).</summary>
        public static void RecordRecovered(string name, string stage)
        {
            Recovered.Add(name);
            RecoveredStages.Add(stage);
        }
    }
}
