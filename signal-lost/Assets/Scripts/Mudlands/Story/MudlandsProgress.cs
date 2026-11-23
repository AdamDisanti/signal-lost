using System;
using System.Collections.Generic;
using UnityEngine;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// The Mudlands' progress as a set of flags ("line1.permit", "kong.beaten"...), the shape the save branch's
    /// <c>SaveData.flags</c> stores, so a save hook only has to copy them (docs/mudlands.md, Saving). Session-only on
    /// main, like the rest of main's progress (StageProgress).
    /// </summary>
    public static class MudlandsProgress
    {
        /// <summary>The stage name ship parts are counted under.</summary>
        public const string Stage = "Mudlands";
        /// <summary>The stage's main ship part (from the stage boss).</summary>
        public const string MainPart = "Landing Gear";
        /// <summary>The stage the Mudlands' boat sails on to (11-22: the Mudlands is the second stage, then the desert).</summary>
        public const string NextStage = SignalLost.Progression.StageProgress.Desert;
        /// <summary>
        /// Set once the Learner's Permit has been earned. The pockets don't carry between scenes yet (the save system's
        /// job), so this flag is what keeps Drunk Driving unlocked on every stage after the Mudlands in the same session.
        /// </summary>
        public const string PermitFlag = "permit";

        private static readonly HashSet<string> Flags = new HashSet<string>();

        public static event Action<string> FlagSet;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Flags.Clear();
            FlagSet = null;
        }

        public static bool Has(string flag) => Flags.Contains(flag);

        public static void Set(string flag)
        {
            if (Flags.Add(flag)) FlagSet?.Invoke(flag);
        }

        /// <summary>Every flag, for a save.</summary>
        public static List<string> CaptureFlags() => new List<string>(Flags);

        /// <summary>Puts saved flags back (call before the story starts).</summary>
        public static void RestoreFlags(IEnumerable<string> saved)
        {
            Flags.Clear();
            if (saved != null) foreach (string flag in saved) Flags.Add(flag);
        }
    }
}
