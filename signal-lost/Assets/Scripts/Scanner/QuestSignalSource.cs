using System.Collections.Generic;
using UnityEngine;

namespace SignalLost.Scanner
{
    /// <summary>
    /// A quest objective the scanner points at (meeting notes 2026-10-20): an NPC with a quest waiting, an item to
    /// find, or the creature a quest asks for. While any quest signal is enabled the scanner ignores creature
    /// signals and tracks the nearest quest signal at any distance; the strength meter still fills only within
    /// range. Quest scripts switch these on and off (component enabled) as objectives change.
    /// </summary>
    public sealed class QuestSignalSource : TrackableTarget
    {
        private static readonly List<QuestSignalSource> Active = new List<QuestSignalSource>();

        /// <summary>The nearest enabled quest signal, or null when no quest is asking for anything.</summary>
        public static QuestSignalSource Nearest(Vector3 from)
        {
            QuestSignalSource best = null;
            float bestDistance = float.MaxValue;
            foreach (QuestSignalSource source in Active)
            {
                float distance = (source.transform.position - from).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = source; }
            }
            return best;
        }

        private void OnEnable() => Active.Add(this);

        protected override void OnDisable()
        {
            base.OnDisable();
            Active.Remove(this);
        }

        // Domain reload may be disabled in the editor; start every Play session with an empty list.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();
    }
}
