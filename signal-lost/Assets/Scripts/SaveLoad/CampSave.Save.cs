using System.Collections.Generic;

namespace SignalLost.Progression
{
    // Save and load (Christian, 10-27): the save-only part of CampSave. The rest is in Progression/CampSave.cs.
    // Each save slot keeps its own copy of the camp's trophies; loading a slot puts that copy back.
    public static partial class CampSave
    {
        /// <summary>Copies the placed trophies into a save slot's lists.</summary>
        public static void Capture(List<string> trophies, List<int> stands)
        {
            trophies.AddRange(Current.trophies);
            stands.AddRange(Current.stands);
        }

        /// <summary>Loading a save slot: the camp shows exactly that slot's trophies.</summary>
        public static void Restore(List<string> trophies, List<int> stands)
        {
            data = new Data();
            if (trophies != null && stands != null)
            {
                for (int i = 0; i < trophies.Count && i < stands.Count; i++)
                {
                    if (string.IsNullOrEmpty(trophies[i]) || data.trophies.Contains(trophies[i]) || data.stands.Contains(stands[i])) continue;
                    data.trophies.Add(trophies[i]);
                    data.stands.Add(stands[i]);
                }
            }
            Write();
            Changed?.Invoke();
        }
    }
}
