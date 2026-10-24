using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SignalLost.Progression
{
    /// <summary>
    /// A boss trophy (meeting notes 10-23): a recognisable piece of a beaten boss that the player carries home and
    /// sets on a stand at camp. The model is loaded from <c>Resources/SignalLost/Trophies/&lt;Id&gt;</c>.
    /// Append new bosses' trophies here; never reuse an Id (the save file stores them).
    /// </summary>
    public sealed class Trophy
    {
        public string Id, Name, Boss;

        public static readonly Trophy[] All =
        {
            new Trophy { Id = "chief_toad_crown", Name = "Chief Toad's Crown", Boss = "Chief Toad" }
        };

        public static Trophy Find(string id)
        {
            foreach (Trophy trophy in All) if (trophy.Id == id) return trophy;
            return null;
        }

        public GameObject LoadModel() => Resources.Load<GameObject>("SignalLost/Trophies/" + Id);
    }

    /// <summary>
    /// What the base camp remembers between play sessions: which trophy sits on which stand. The camp is the same in
    /// every level, so every level's camp shows them. Saved as a small JSON file in the player's data folder; New Game
    /// clears it. Each save slot keeps its own copy (<see cref="SaveGame"/>): loading a slot puts that copy back.
    /// </summary>
    public static class CampSave
    {
        [Serializable]
        private sealed class Data
        {
            public List<string> trophies = new List<string>();
            public List<int> stands = new List<int>();
        }

        private static Data data;

        /// <summary>Raised after a trophy is placed or the save is cleared.</summary>
        public static event Action Changed;

        public static string FilePath => Path.Combine(Application.persistentDataPath, "signal_lost_camp.json");

        private static Data Current
        {
            get
            {
                if (data != null) return data;
                data = new Data();
                try
                {
                    if (File.Exists(FilePath)) data = JsonUtility.FromJson<Data>(File.ReadAllText(FilePath)) ?? new Data();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Could not read the camp save; starting fresh. " + exception.Message);
                }
                return data;
            }
        }

        /// <summary>The trophy on this stand, or null if it is empty.</summary>
        public static string TrophyOn(int stand)
        {
            int index = Current.stands.IndexOf(stand);
            return index >= 0 ? Current.trophies[index] : null;
        }

        public static bool IsPlaced(string trophyId) => Current.trophies.Contains(trophyId);

        public static void Place(string trophyId, int stand)
        {
            if (IsPlaced(trophyId) || TrophyOn(stand) != null) return;
            Current.trophies.Add(trophyId);
            Current.stands.Add(stand);
            Write();
            Changed?.Invoke();
        }

        /// <summary>New Game: forget every placed trophy.</summary>
        public static void Clear()
        {
            data = new Data();
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch (Exception exception) { Debug.LogWarning("Could not clear the camp save. " + exception.Message); }
            Changed?.Invoke();
        }

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

        private static void Write()
        {
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(Current, true)); }
            catch (Exception exception) { Debug.LogWarning("Could not write the camp save. " + exception.Message); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            data = null;
            Changed = null;
        }
    }
}
