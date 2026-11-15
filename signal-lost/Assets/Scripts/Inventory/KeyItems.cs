using System.Collections.Generic;

namespace SignalLost.Inventory
{
    /// <summary>
    /// Display names for key items (pockets ids such as "learners_permit"). Trophies have their own names
    /// (<see cref="SignalLost.Progression.Trophy"/>); anything unknown shows its id.
    /// </summary>
    public static class KeyItems
    {
        public const string LearnersPermit = "learners_permit";
        public const string KartWheel = "kart_wheel";
        public const string MudstoneBead = "mudstone_bead";
        public const string WitchEmber = "witch_ember";

        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            [LearnersPermit] = "Learner's Permit",
            [KartWheel] = "Kart Wheel",
            [MudstoneBead] = "Mudstone Bead",
            [WitchEmber] = "Witch Ember"
        };

        public static string Name(string id)
        {
            if (id == null) return "";
            if (Names.TryGetValue(id, out string name)) return name;
            return SignalLost.Progression.Trophy.Find(id)?.Name ?? id;
        }
    }
}
